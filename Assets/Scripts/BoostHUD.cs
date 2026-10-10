using TMPro;
using UnityEngine;
using FishNet.Object;
using StarterAssets;

public class BoostHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text boostText;

    private PlayerBoosts playerBoosts;

    private void Awake()
    {
        if (boostText != null)
        {
            boostText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerBoosts == null)
        {
            FindLocalPlayerBoosts();
            return;
        }

        if (
            playerBoosts.ActiveBoost ==
            PlayerBoosts.BoostType.None ||
            playerBoosts.TimeRemaining <= 0f
        )
        {
            boostText.gameObject.SetActive(false);
            return;
        }

        boostText.gameObject.SetActive(true);

        int seconds =
            Mathf.CeilToInt(
                playerBoosts.TimeRemaining
            );

        boostText.text =
            GetBoostName(
                playerBoosts.ActiveBoost
            ) +
            "\n" +
            seconds +
            " seconds";
    }

    private void FindLocalPlayerBoosts()
    {
        PlayerBoosts[] allBoosts =
            FindObjectsByType<PlayerBoosts>(
                FindObjectsSortMode.None
            );

        foreach (PlayerBoosts boost in allBoosts)
        {
            NetworkObject networkObject =
                boost.GetComponentInParent<NetworkObject>();

            if (
                networkObject != null &&
                networkObject.IsOwner
            )
            {
                playerBoosts = boost;
                return;
            }
        }
    }

    private string GetBoostName(
        PlayerBoosts.BoostType boostType
    )
    {
        switch (boostType)
        {
            case PlayerBoosts.BoostType.InfiniteHealth:
                return "Infinite Health";

            case PlayerBoosts.BoostType.Speed:
                return "Speed Boost";

            case PlayerBoosts.BoostType.Jump:
                return "Jump Boost";

            case PlayerBoosts.BoostType.Damage:
                return "Damage Boost";

            default:
                return "Boost";
        }
    }
}