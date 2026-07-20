using UnityEngine;
using UnityEngine.InputSystem;

public class HitscanShooter : MonoBehaviour
{
    public int damage = 40;
    public float range = 100f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        Vector3 crosshairPosition = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        Ray ray = cam.ScreenPointToRay(crosshairPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            Health health = hit.collider.GetComponentInParent<Health>();

            if (health != null)
            {
                health.TakeDamage(damage);
            }
        }

        Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 1f);
    }
}