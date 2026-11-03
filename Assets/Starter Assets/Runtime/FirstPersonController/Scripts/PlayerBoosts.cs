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
            Damage,
            Armor,
            Invisibility
        }

        [Header("Boost Settings")]
        [SerializeField] private float boostDuration = 20f;

        [Header("Boost Multipliers")]
        [SerializeField] private float speedMultiplier = 1.5f;
        [SerializeField] private float jumpMultiplier = 1.5f;
        [SerializeField] private float damageMultiplier = 2f;

        [Header("Armor Pickup")]
        [SerializeField] private int armorAmount = 100;

        public BoostType ActiveBoost { get; private set; }
        public float TimeRemaining { get; private set; }
        public bool IsInvisible { get; private set; }

        public bool IsInvulnerable =>
            ActiveBoost == BoostType.InfiniteHealth &&
            TimeRemaining > 0f;

        public float SpeedMultiplier =>
            ActiveBoost == BoostType.Speed && TimeRemaining > 0f
                ? speedMultiplier
                : 1f;

        public float JumpMultiplier =>
            ActiveBoost == BoostType.Jump && TimeRemaining > 0f
                ? jumpMultiplier
                : 1f;

        public float DamageMultiplier =>
            ActiveBoost == BoostType.Damage && TimeRemaining > 0f
                ? damageMultiplier
                : 1f;

        private Renderer[] playerRenderers;
        private bool[] originalRendererStates;

        private void Awake()
        {
            playerRenderers = GetComponentsInChildren<Renderer>(true);
            originalRendererStates = new bool[playerRenderers.Length];

            for (int i = 0; i < playerRenderers.Length; i++)
                originalRendererStates[i] = playerRenderers[i].enabled;
        }

        private void Update()
        {
            if (ActiveBoost == BoostType.None)
                return;

            TimeRemaining -= Time.deltaTime;

            if (TimeRemaining <= 0f)
                ClearBoost();
        }

        public void ApplyBoost(BoostType boostType)
        {
            if (boostType == BoostType.Armor)
            {
                gameObject.SendMessageUpwards(
                    "AddArmor",
                    armorAmount,
                    SendMessageOptions.DontRequireReceiver
                );
                return;
            }

            ActiveBoost = boostType;
            TimeRemaining = boostDuration;

            SetInvisible(boostType == BoostType.Invisibility);

            Debug.Log(
                gameObject.name + " collected " + boostType +
                " for " + boostDuration + " seconds."
            );
        }

        private void SetInvisible(bool invisible)
        {
            IsInvisible = invisible;

            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i] != null)
                    playerRenderers[i].enabled =
                        invisible ? false : originalRendererStates[i];
            }
        }

        public void ClearBoost()
        {
            SetInvisible(false);

            ActiveBoost = BoostType.None;
            TimeRemaining = 0f;

            Debug.Log(gameObject.name + "'s boost expired.");
        }
    }
}