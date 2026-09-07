using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Put down on the table in front of them. It comes from nowhere in particular, so it ignores the
	// source: this is the arrival for an item that was already theirs.
	[CreateAssetMenu(fileName = "ItemArrival_Drop", menuName = "Game/Poker/Item Arrivals/Drop")]
	public class PokerItemDropArrival : PokerItemArrival
	{
		[Header("Drop")]
		[Tooltip("How far above its place it starts. It is put down rather than appearing, so the table can see it arrive.")]
		[SerializeField] private float _height = 0.35f;

		[SerializeField] private Ease _ease = Ease.OutBounce;

		protected override void OnPlay(Transform item, Vector3 resting, Transform source)
		{
			item.localPosition = resting + Vector3.up * _height;
			item.DOLocalMove(resting, Duration).SetEase(_ease);
		}
	}
}
