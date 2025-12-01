using UnityEngine;

public class SonarController : MonoBehaviour
{
    public Transform sonarOrigin;
    public Vector3 direction = Vector3.forward;
    public float maxDistance = 100f;
    public LayerMask terrainMask;

    [SerializeField] public GameObject sonarDisplay;

    [Header("Sonar Arc Settings")]
    [SerializeField] private int rayCount = 20;
    [SerializeField] private float arcAngle = 90f;

    [SerializeField] public GameObject collisionPointSphere;

    [Header("Sphere container")]
    [SerializeField] private Transform sphereParent;

    public void Awake()
    {
        terrainMask = LayerMask.GetMask("Terrain");
    }

    public float Ping()
    {
        float minDistance = maxDistance;
        Vector3 closestPoint = sonarOrigin.position + sonarOrigin.TransformDirection(direction) * maxDistance;
        bool hitSomething = false;

        float halfArc = arcAngle / 2f;

        for (int i = 0; i < rayCount; i++)
        {
            float lerp = (float)i / (rayCount - 1);
            float angle = Mathf.Lerp(-halfArc, halfArc, lerp);
            Quaternion rot = Quaternion.AngleAxis(angle, sonarOrigin.up);
            Vector3 rayDir = rot * sonarOrigin.TransformDirection(direction);

            Debug.DrawRay(sonarOrigin.position, rayDir * maxDistance, Color.red, 1.0f);

            RaycastHit hit;
            if (Physics.Raycast(sonarOrigin.position, rayDir, out hit, maxDistance, terrainMask))
            {
                if (collisionPointSphere != null)
                {
                    Vector3 spawnPos = hit.point + hit.normal * 0.01f;

                    GameObject instance = Instantiate(
                        collisionPointSphere,
                        spawnPos,
                        Quaternion.identity,
                        sphereParent
                    );

                    Destroy(instance, 5f);
                }

                if (hit.distance < minDistance)
                {
                    minDistance = hit.distance;
                    closestPoint = hit.point;
                    hitSomething = true;
                }
            }
        }

        return hitSomething ? minDistance : 99f;
    }
}
