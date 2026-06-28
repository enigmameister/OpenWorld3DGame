using UnityEngine;

public static class HitFeedbackUtility
{
    public static void PlayHitFx(
        Transform npcRoot,
        GameObject bloodFxPrefab,
        AudioClip hurtSfx,
        Vector3? hitPointWorld = null,
        Vector3? hitNormalWorld = null,
        float bloodFxScale = 1f,
        float bloodFxLifetime = 2f,
        AudioSource reuseAudioSource = null)
    {
        // 1) KREW (jeœli prefab jest podany)
        if (bloodFxPrefab != null)
        {
            Vector3 pos = hitPointWorld ?? (npcRoot.position + Vector3.up * 1.2f);
            Quaternion rot = Quaternion.LookRotation(hitNormalWorld ?? Vector3.up);
            var fx = Object.Instantiate(bloodFxPrefab, pos, rot);
            fx.transform.localScale *= Mathf.Max(0.01f, bloodFxScale);
            Object.Destroy(fx, Mathf.Max(0.1f, bloodFxLifetime));
        }

        // 2) SFX (jeœli podany)
        if (hurtSfx != null)
        {
            if (reuseAudioSource != null)
            {
                reuseAudioSource.PlayOneShot(hurtSfx);
            }
            else
            {
                AudioSource.PlayClipAtPoint(hurtSfx, npcRoot.position, 0.9f);
            }
        }
    }
}
