using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Steamworks;
using Steamworks.Data;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Steam
{
    using SocketConnection = Connection;

    public class FacepunchTransport : NetworkTransport, IConnectionManager, ISocketManager
    {
        private ConnectionManager _connectionManager;
        private SocketManager _socketManager;
        private Dictionary<ulong, Client> _connectedClients;
        private bool _steamInitialized;

        [ReadOnly]
        [Tooltip("The Steam ID of the user targeted when joining as a client.")]
        [SerializeField] public ulong TargetSteamId;

        private LogLevel LogLevel => NetworkManager.Singleton.LogLevel;

        private class Client
        {
            public SteamId SteamId;
            public SocketConnection Connection;
        }

        #region NetworkTransport Overrides

        protected override void OnEarlyUpdate()
        {
            if (!_steamInitialized && SteamClient.IsValid)
            {
                _steamInitialized = true;
                SteamNetworkingUtils.InitRelayNetworkAccess();

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Initialized access to Steam Relay Network.");

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Fetched user Steam ID.");
            }
        }

        public override ulong ServerClientId => 0;

        public override void DisconnectLocalClient()
        {
            _connectionManager?.Connection.Close();

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnecting local client.");
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (_connectedClients.TryGetValue(clientId, out Client user))
            {
                user.Connection.Flush();
                user.Connection.Close();
                _connectedClients.Remove(clientId);

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnecting remote client with ID {clientId}.");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to disconnect remote client with ID {clientId}, client not connected.");
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            return 0;
        }

        public override void Initialize(NetworkManager networkManager = null)
        {
            _connectedClients ??= new Dictionary<ulong, Client>();
            _connectedClients.Clear();
        }

        private SendType NetworkDeliveryToSendType(NetworkDelivery delivery)
        {
            return delivery switch
            {
                NetworkDelivery.Reliable => SendType.Reliable,
                NetworkDelivery.ReliableFragmentedSequenced => SendType.Reliable,
                NetworkDelivery.ReliableSequenced => SendType.Reliable,
                NetworkDelivery.Unreliable => SendType.Unreliable,
                NetworkDelivery.UnreliableSequenced => SendType.Unreliable,
                _ => SendType.Reliable
            };
        }

        public override void Shutdown()
        {
            try
            {
                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Shutting down.");

                _connectionManager?.Close();
                _socketManager?.Close();
            }
            catch (Exception e)
            {
                if (LogLevel <= LogLevel.Error)
                    Debug.LogError($"[{nameof(FacepunchTransport)}] - Caught an exception while shutting down: {e}");
            }
        }

        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
	        var sendType = NetworkDeliveryToSendType(delivery);

	        if (clientId == ServerClientId)
		        _connectionManager.Connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
	        else if (_connectedClients.TryGetValue(clientId, out Client user))
		        user.Connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
	        else if (LogLevel <= LogLevel.Normal)
		        Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to send packet to remote client with ID {clientId}, client not connected.");
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            _connectionManager?.Receive();
            _socketManager?.Receive();

            clientId = 0;
            receiveTime = Time.realtimeSinceStartup;
            payload = default;
            return NetworkEvent.Nothing;
        }

        public override bool StartClient()
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as client.");

            _connectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(TargetSteamId);
            _connectionManager.Interface = this;
            return true;
        }

        public override bool StartServer()
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as server.");

            _socketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
            _socketManager.Interface = this;
            return true;
        }

        #endregion

        #region ConnectionManager Implementation

        private byte[] _payloadCache = new byte[4096];

        private void EnsurePayloadCapacity(int size)
        {
            if (_payloadCache.Length >= size)
                return;

            _payloadCache = new byte[Math.Max(_payloadCache.Length * 2, size)];
        }

        void IConnectionManager.OnConnecting(ConnectionInfo info)
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Connecting with Steam user {info.Identity.SteamId}.");
        }

        void IConnectionManager.OnConnected(ConnectionInfo info)
        {
            InvokeOnTransportEvent(NetworkEvent.Connect, ServerClientId, default, Time.realtimeSinceStartup);

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Connected with Steam user {info.Identity.SteamId}.");
        }

        void IConnectionManager.OnDisconnected(ConnectionInfo info)
        {
            InvokeOnTransportEvent(NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup);

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}.");
        }

        unsafe void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            EnsurePayloadCapacity(size);

            fixed (byte* payload = _payloadCache)
            {
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            }

            InvokeOnTransportEvent(NetworkEvent.Data, ServerClientId, new ArraySegment<byte>(_payloadCache, 0, size), Time.realtimeSinceStartup);
        }

        #endregion

        #region SocketManager Implementation

        void ISocketManager.OnConnecting(SocketConnection connection, ConnectionInfo info)
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Accepting connection from Steam user {info.Identity.SteamId}.");

            connection.Accept();
        }

        void ISocketManager.OnConnected(SocketConnection connection, ConnectionInfo info)
        {
            if (!_connectedClients.ContainsKey(connection.Id))
            {
                _connectedClients.Add(connection.Id, new Client()
                {
                    Connection = connection,
                    SteamId = info.Identity.SteamId
                });

                InvokeOnTransportEvent(NetworkEvent.Connect, connection.Id, default, Time.realtimeSinceStartup);

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Connected with Steam user {info.Identity.SteamId}.");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to connect client with ID {connection.Id}, client already connected.");
        }

        void ISocketManager.OnDisconnected(SocketConnection connection, ConnectionInfo info)
        {
            if (_connectedClients.Remove(connection.Id))
	    {
	        InvokeOnTransportEvent(NetworkEvent.Disconnect, connection.Id, default, Time.realtimeSinceStartup);

	       if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}");
	    }
     	    else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to diconnect client with ID {connection.Id}, client not connected.");
        }

        unsafe void ISocketManager.OnMessage(SocketConnection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            EnsurePayloadCapacity(size);

            fixed (byte* payload = _payloadCache)
            {
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            }

            InvokeOnTransportEvent(NetworkEvent.Data, connection.Id, new ArraySegment<byte>(_payloadCache, 0, size), Time.realtimeSinceStartup);
        }

        #endregion
    }
}
