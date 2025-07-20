using UnityEngine;

public class VacuumSuction : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Debu"))
        {
            StartCoroutine(Sedot(other.gameObject));
        }
    }

    System.Collections.IEnumerator Sedot(GameObject debu)
    {
        Vector3 start = debu.transform.position;
        Vector3 end = transform.parent.position;
        float waktu = 0f;
        float durasi = 0.5f;

        while (waktu < durasi)
        {
            if (debu == null) yield break;
            debu.transform.position = Vector3.Lerp(start, end, waktu / durasi);
            waktu += Time.deltaTime;
            yield return null;
        }

        Destroy(debu);
        Debug.Log("Debu sedot");
    }
}
