﻿using UnityEngine;

namespace AM.Events;

[CreateAssetMenu(fileName = "GoreSplash", menuName = "Events/GoreSplash")]
public class GoreSplashEvent : EventBase
{
    public override string EventID => "GoreSplash";

    public int AroundPawnIndex = 1;
    public int Count = 5;
    public float Radius = 0.5f;

    protected override void Expose()
    {
        Look(ref AroundPawnIndex);
        Look(ref Count);
        Look(ref Radius);
    }
}