using UnityEngine;

public class BlockGenerator : ObjectGenerator
{
    [SerializeField] private GameObject blockPrefab;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Vector3 blockPosition_leftSide = new Vector3(Random.Range(-7.0f, -2.0f), Random.Range(-3.0f, -1.0f));
        Vector3 blockPosition_rightSide = new Vector3(Random.Range(2.0f, 7.0f), Random.Range(0.0f, 3.0f));
        GenerateObject(blockPrefab, blockPosition_leftSide);
        GenerateObject(blockPrefab, blockPosition_rightSide);
    }
}
