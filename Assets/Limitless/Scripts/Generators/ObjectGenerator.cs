using UnityEngine;

public class ObjectGenerator : MonoBehaviour
{
    public virtual GameObject GenerateObject(GameObject prefab, Vector3 position)
    {
        GameObject obj = Instantiate(prefab, position, Quaternion.identity);

        return obj;
    }
}
