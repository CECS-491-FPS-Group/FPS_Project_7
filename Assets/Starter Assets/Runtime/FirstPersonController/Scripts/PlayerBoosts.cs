using UnityEngine;

namespace StarterAssets
{
    public class PlayerBoosts : MonoBehaviour
    {
        public enum BoostType
        {
            None,
            InfiniteHealth,
            Speed,
            Jump,
            Damage
        }

        [Header("Boost Settings")]
        [SerializeField] private float boostDuration = 20f;

        [Header("Boost Multipliers")]
        [SerializeField] private float speedMultiplier = 1.5f;
        [SerializeField] private float jumpMultiplier = 1.5f;
        [SerializeField] private float damageMultiplier = 2f;

        public BoostType ActiveBoost { get; private set; }

        public float TimeRemaining { get; private set; }

        public bool IsInvulnerable =>
            ActiveBoost == BoostType.InfiniteHealth &&
            TimeRemaining > 0f;

        public float SpeedMultiplier =>
            ActiveBoost == BoostType.Speed &&
            TimeRemaining > 0f
                ? speedMultiplier
                : 1f;

        public float JumpMultiplier =>
            ActiveBoost == BoostType.Jump &&
            TimeRemaining > 0f
                ? jumpMultiplier
                : 1f;

        public float DamageMultiplier =>
            ActiveBoost == BoostType.Damage &&
            TimeRemaining > 0f
                ? damageMultiplier
                : 1f;

        private void Update()
        {
            if (ActiveBoost == BoostType.None)
                return;

            TimeRemaining -= Time.deltaTime;

            if (TimeRemaining <= 0f)
            {
                ClearBoost();
            }
        }

        public void ApplyBoost(BoostType boostType)
        {
            ActiveBoost = boostType;
            TimeRemaining = boostDuration;

            Debug.Log(
                gameObject.name +
                " collected " +
                boostType +
                " for " +
                boostDuration +
                " seconds."
            );
        }

        public void ClearBoost()
        {
            ActiveBoost = BoostType.None;
            TimeRemaining = 0f;

            Debug.Log(
                gameObject.name +
                "'s boost expired."
            );
        }
    }
}