using UnityEngine;

public class VacuumSuction : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Debu"))
        {
            DebuObject d = other.GetComponent<DebuObject>();
            if (d != null && !d.sedangDisedot)
            {
                d.sedangDisedot = true;
                StartCoroutine(Sedot(other.gameObject));
            }
        }
    }

    System.Collections.IEnumerator Sedot(GameObject debu)
    {
        Vector3 start = debu.transform.position;
        Vector3 end = transform.parent.position; // Robot position
        float waktu = 0f;
        float durasi = 0.5f;

        while (waktu < durasi)
        {
            if (debu == null) yield break;

            Transform t = debu.transform;
            if (t != null)
                t.position = Vector3.Lerp(start, end, waktu / durasi);

            waktu += Time.deltaTime;
            yield return null;
        }

        Destroy(debu);
        GameManager.Instance.AddScore(1);
    }
}
