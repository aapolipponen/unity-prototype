using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.UIElements;
using System.Collections;
using Unity.VisualScripting;
using System.Numerics;
[AddComponentMenu("Camera Stuff")]
public class FollowEntity : MonoBehaviour
{

    public Entity entitytofollow;
    private EntityManager manager;
    public float3 offset;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private IEnumerator Start()
    {
        manager = World.DefaultGameObjectInjectionWorld.EntityManager;
        yield return new WaitForSeconds(0.5f);
        entitytofollow = manager.CreateEntityQuery(typeof(BeFollowed)).GetSingletonEntity();
        transform.rotation = manager.GetComponentData<LocalTransform>(entitytofollow).Rotation;
        offset = manager.GetComponentData<BeFollowed>(entitytofollow).offset;
    }
    // Update is called once per frame
    void LateUpdate()
    {
        if (entitytofollow.Index == 0) { return; }
        transform.position = manager.GetComponentData<LocalToWorld>(entitytofollow).Position - manager.GetComponentData<LocalToWorld>(entitytofollow).Forward *5f - offset;
        transform.rotation = manager.GetComponentData<LocalToWorld>(entitytofollow).Rotation;
    }


}

