using System;
using UnityEngine;

public enum BallType { Normal, Red, Blue }

[Serializable]
public class Ball
{
    [field: SerializeField] public BallType Type { get; private set; }

    [field: SerializeField] public GameObject BallPrefab { get; private set; }

    [field: SerializeField] public float Rate { get; private set; }
}
