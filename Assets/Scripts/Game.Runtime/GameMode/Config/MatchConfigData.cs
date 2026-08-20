using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Config
{
	// The replicated store behind every match-config surface. The mode registers its entries on every
	// peer; the server seeds one list element per entry (the room screen's pending choice, else the
	// authored default) and each peer pushes arriving values back into its own entries — stages are
	// cloned per peer, so a value only the server applied would never reach the numbers client UI reads.
	public class MatchConfigData : NetworkBehaviour
	{
		public NetworkList<MatchConfigValue> Values { get; } = new(
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public event Action OnValuesChanged;

		private readonly List<MatchConfigEntry> _entries = new();
		private readonly Dictionary<string, MatchConfigEntry> _entriesById = new();
		private readonly Dictionary<string, float> _valuesById = new();
		private Func<bool> _serverCanEdit;
		private bool _replicationInitialized;

		public IReadOnlyList<MatchConfigEntry> Entries => _entries;

		// Registration and spawn race by component order, so whichever lands second does the wiring.
		public void RegisterEntries(List<MatchConfigEntry> entries, Func<bool> serverCanEdit)
		{
			_entries.Clear();
			_entriesById.Clear();
			_serverCanEdit = serverCanEdit;

			foreach (var entry in entries)
			{
				if (entry == null) continue;

				if (_entriesById.ContainsKey(entry.Id))
				{
					Debug.LogWarning($"[MatchConfigData] Duplicate config id '{entry.Id}' — keeping the first.");
					continue;
				}

				_entries.Add(entry);
				_entriesById.Add(entry.Id, entry);
			}

			if (IsSpawned) InitializeReplication();
		}

		public override void OnNetworkSpawn()
		{
			if (_entries.Count > 0) InitializeReplication();
		}

		public override void OnNetworkDespawn()
		{
			Values.OnListChanged -= HandleValuesChanged;

			// The entries close over stage clones the machine is about to destroy.
			_entries.Clear();
			_entriesById.Clear();
			_valuesById.Clear();
			_serverCanEdit = null;
			_replicationInitialized = false;
		}

		public bool TryGetValue(string id, out float value) => _valuesById.TryGetValue(id, out value);

		[Rpc(SendTo.Server)]
		public void SubmitConfigValueRPC(FixedString64Bytes id, float value, RpcParams rpcParams = default)
		{
			var sender = rpcParams.Receive.SenderClientId;
			if (sender != NetworkManager.ServerClientId)
			{
				Debug.LogWarning($"[MatchConfigData] Refused config edit from client {sender}: only the host edits.");
				return;
			}

			if (_serverCanEdit != null && !_serverCanEdit())
			{
				Debug.LogWarning($"[MatchConfigData] Refused config edit '{id}': match config is locked outside the waiting phase.");
				return;
			}

			var key = id.ToString();
			if (!_entriesById.TryGetValue(key, out var entry))
			{
				Debug.LogWarning($"[MatchConfigData] Refused config edit '{key}': unknown config id.");
				return;
			}

			var clamped = entry.ClampValue(value);

			for (var i = 0; i < Values.Count; i++)
			{
				if (!Values[i].Id.Equals(id)) continue;
				if (Mathf.Approximately(Values[i].Value, clamped)) return;

				Values[i] = new MatchConfigValue { Id = id, Value = clamped };
				return;
			}
		}

		private void InitializeReplication()
		{
			if (_replicationInitialized) return;

			_replicationInitialized = true;

			Values.OnListChanged += HandleValuesChanged;

			if (IsServer && Values.Count == 0)
			{
				foreach (var entry in _entries)
				{
					var value = PendingMatchConfig.TryGet(entry.Id, out var pending)
						? entry.ClampValue(pending)
						: entry.ReadValue();

					Values.Add(new MatchConfigValue { Id = entry.Id, Value = value });
				}

				PendingMatchConfig.Clear();
			}

			// The initial list sync raises no change event, so a client snaps to what already stands.
			ApplyAllValues();
		}

		private void ApplyAllValues()
		{
			_valuesById.Clear();

			foreach (var stored in Values)
			{
				var id = stored.Id.ToString();
				_valuesById[id] = stored.Value;

				if (_entriesById.TryGetValue(id, out var entry)) entry.ApplyValue(stored.Value);
			}

			OnValuesChanged?.Invoke();
		}

		private void HandleValuesChanged(NetworkListEvent<MatchConfigValue> change)
		{
			if (change.Type is NetworkListEvent<MatchConfigValue>.EventType.Add
				or NetworkListEvent<MatchConfigValue>.EventType.Insert
				or NetworkListEvent<MatchConfigValue>.EventType.Value)
			{
				var id = change.Value.Id.ToString();
				_valuesById[id] = change.Value.Value;

				if (_entriesById.TryGetValue(id, out var entry)) entry.ApplyValue(change.Value.Value);

				OnValuesChanged?.Invoke();
				return;
			}

			// Nothing removes or clears today; a full resync is the safe answer if something ever does.
			ApplyAllValues();
		}
	}
}
