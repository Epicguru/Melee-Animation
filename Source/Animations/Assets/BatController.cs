using System.Collections.Generic;
using UnityEngine;

public class BatController : MonoBehaviour
{
    public Animator Anim;
    public float BlockTime = 0.25f;
    public float NormalSwingSpeed = 1f;
    public float HitPauseTime = 0.1f;
    public int MaxPausesPerSwing = 1;
    public Transform HitStart, HitEnd;
    public GameObject HitEffectPrefab;

    private float blockLerp;
    private float pauseTimer;
    private HashSet<Collider> hitCollidersThisSwing = new HashSet<Collider>();

    private void Update()
    {
        DetectHits();

        bool swing = Input.GetMouseButtonDown(0);
        bool block = Input.GetMouseButton(1);

        if(swing && !block)
        {
            Anim.SetTrigger("Swing");
        }
        else if (block)
        {
            Anim.ResetTrigger("Swing");
        }

        blockLerp = Mathf.MoveTowards(blockLerp, block ? 1f : 0f, Time.deltaTime * (1f / BlockTime));
        Anim.SetLayerWeight(1, blockLerp);

        Anim.SetFloat("SwingSpeed", pauseTimer > 0 ? 0 : NormalSwingSpeed);

        pauseTimer -= Time.deltaTime;
        if (pauseTimer < 0)
            pauseTimer = 0;
    }

    public void OnSwingStart()
    {
        hitCollidersThisSwing.Clear();
        pauseTimer = 0;
    }

    private void DetectHits()
    {
        if(Physics.Linecast(HitStart.position, HitEnd.position, out var hit))
        {
            if (hitCollidersThisSwing.Add(hit.collider))
            {
                // HIT IT!
                Debug.Log($"Hit {hit.collider.gameObject.name}!");

                // Check if should pause.
                if(hitCollidersThisSwing.Count <= MaxPausesPerSwing)
                {
                    PauseSwing();
                }

                var spawned = Instantiate(HitEffectPrefab);
                spawned.transform.position = hit.point + hit.normal * 0.1f;
                Destroy(spawned, 4);
            }
        }
    }

    private void PauseSwing()
    {
        pauseTimer = HitPauseTime;
    }
}
