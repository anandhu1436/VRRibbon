﻿using System.Collections.Generic;
using UnityEngine;


public class Sample
{
    public Vector3 position;
    public Vector3 normal;
    public Vector3 tangent;
    public Vector3 width;
    public float pressure;
    public Vector3 velocity;
    public float CreationTime { get; }

    public Sample(Vector3 position, Vector3 normal, Vector3 tangent,Vector3 brushWidth, float pressure, Vector3 velocity)
    {
        this.position = position;
        this.normal = normal;
        this.tangent = tangent;
        this.pressure = pressure;
        this.velocity = velocity;
        this.width=brushWidth;
        this.CreationTime = Time.time;

    }

    public void LaplacianSmooth(Sample sA, Sample sB)
    {
        position += 0.5f * (sA.position - position) + 0.5f * (sB.position - position);
    }

    
}