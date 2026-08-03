using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;



// from how things look, there will be separate shooting codes for each weapon and they will gnerally follow this sccript (correct me if im wrong)
// each individual weapon will have different recoil, damaage, and hitmarkers appropriate to the weapon, and currently all thsese properties are adjustable in the inspector.
// right now its all about polishing thw code so that it looks correct.

public class HitscanShooter : MonoBehaviour
{

    private Camera cam;


    // Damage
    public int damage = 40;
    public float range = 100f;

    // Recoil
    public float recoilAmount = 2f;
    public float recoilSpeed = 10f;
    public float returnSpeed = 5f;

    private Vector3 originalRotation;
    private float currentRecoil;

    // Hitmarker (use a queue to determine which hitmarker to destroy after 8 are spawned, and since it makes sense to be time based))
    public GameObject hitMarker;
    public int maxHitMarkers = 8;
    private Queue<GameObject> hitMarkers = new Queue<GameObject>();


    // ignore the recoil code for now, i learned that there is a better way to handle recoil. the current version has it so that you dont return to the original position after shooting. so its pretty bad code right now.



    private void Awake()
    {
        cam = GetComponentInChildren<Camera>();


        //used for recoil code. ignore for now. though maygbe it wont be changed i need to check online how to handle recoil so that it returns to position of 1st shot.
        originalRotation = cam.transform.localEulerAngles;
    }

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            Shoot();
        }

        HandleRecoil();
    }



    private void ApplyRecoil()
    {
        currentRecoil -= recoilAmount;
    }


    // i want to say thaat the firat position of the the camera when the first shot is taken will be recorded here. it feels like i will need to have the camera try and "revert" back to the original
    // orientaation gradually. like the camera should naturally return to the base after the last shot is fired. thats the hard part i think. 
    private void HandleRecoil()
    {
        currentRecoil = Mathf.Lerp(
            currentRecoil,
            0,
            recoilSpeed * Time.deltaTime
        );

        cam.transform.localRotation = Quaternion.Euler(
            currentRecoil,
            0,
            0
        );
    }

    private void Shoot()
    {
        Vector3 crosshairPosition = new Vector3(
            Screen.width / 2f,
            Screen.height / 2f,
            0f
        );

        Ray ray = cam.ScreenPointToRay(crosshairPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            // Spawn hit marker
            if (hitMarker != null)
            {
                GameObject marker = Instantiate(
                    hitMarker,
                    hit.point + hit.normal * 0.01f,
                    Quaternion.LookRotation(-hit.normal)
                );

                hitMarkers.Enqueue(marker);

                // Remove oldest marker after 8
                if (hitMarkers.Count > maxHitMarkers)
                {
                    GameObject oldest = hitMarkers.Dequeue();

                    if (oldest != null)
                    {
                        Destroy(oldest);
                    }
                }
            }

            // Damage enemies
            Health health = hit.collider.GetComponentInParent<Health>();

            if (health != null)
            {
                health.TakeDamage(damage);
            }
        }


        ApplyRecoil();

        Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 1f);
    }
}