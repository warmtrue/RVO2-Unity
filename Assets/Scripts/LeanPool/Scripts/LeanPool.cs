using UnityEngine;
using System.Collections.Generic;

namespace Lean
{
	// This component allows you to pool Unity objects for fast instantiation and destruction
	[AddComponentMenu("Lean/Pool")]
	public class LeanPool : MonoBehaviour
	{
		public class DelayedDestruction
		{
			public GameObject Clone;
			
			public float Life;
		}
		
		public enum NotificationType
		{
			None,
			SendMessage,
			BroadcastMessage
		}
		
		// All the currently active pools in the scene
		public static List<LeanPool> AllPools = new List<LeanPool>();
		
		// The reference between a spawned GameObject and its pool
		public static Dictionary<GameObject, LeanPool> AllLinks = new Dictionary<GameObject, LeanPool>();
		
		[Tooltip("The prefab the clones will be based on")]
		public GameObject Prefab;
		
		[Tooltip("Should this pool preload some clones?")]
		public int Preload;
		
		[Tooltip("Should this pool have a maximum amount of spawnable clones?")]
		public int Capacity;
		
		[Tooltip("Should this pool send messages to the clones when they're spawned/despawned?")]
		public NotificationType Notification = NotificationType.SendMessage;
		
		// All the currently cached prefab instances
		private List<GameObject> cache = new List<GameObject>();
		
		// All the delayed destruction objects
		private List<DelayedDestruction> delayedDestructions = new List<DelayedDestruction>();
		
		// The total amount of created prefabs
		private int total;
		
		// These methods allows you to spawn prefabs via Component with varying levels of transform data
		public static T Spawn<T>(T prefab)
			where T : Component
		{
			return Spawn(prefab, Vector3.zero, Quaternion.identity, null);
		}
		
		public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation)
			where T : Component
		{
			return Spawn(prefab, position, rotation, null);
		}
		
		public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent)
			where T : Component
		{
			// Clone this prefabs's GameObject
			var gameObject = prefab != null ? prefab.gameObject : null;
			var clone      = Spawn(gameObject, position, rotation, parent);
			
			// Return the same component from the clone
			return clone != null ? clone.GetComponent<T>() : null;
		}
		
		// These methods allows you to spawn prefabs via GameObject with varying levels of transform data
		public static GameObject Spawn(GameObject prefab)
		{
			return Spawn(prefab, Vector3.zero, Quaternion.identity, null);
		}
		
		public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
		{
			return Spawn(prefab, position, rotation, null);
		}
		
		public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
		{
			if (prefab != null)
			{
				// Find the pool that handles this prefab
				var pool = AllPools.Find(p => p.Prefab == prefab);
				
				// Create a new pool for this prefab?
				if (pool == null)
				{
					pool = new GameObject(prefab.name + " Pool").AddComponent<LeanPool>();
					
					pool.Prefab = prefab;
				}
				
				// Spawn a clone from this pool
				var clone = pool.FastSpawn(position, rotation, parent);
				
				// Was a clone created?
				// NOTE: This will be null if the pool's capacity has been reached
				if (clone != null)
				{
					// Associate this clone with this pool
					AllLinks.Add(clone, pool);
					
					// Return the clone
					return clone.gameObject;
				}
			}
			else
			{
				Debug.LogError("Attempting to spawn a null prefab");
			}
			
			return null;
		}
		
		// This allows you to despawn a clone via Component, with optional delay
		public static void Despawn(Component clone, float delay = 0.0f)
		{
			if (clone != null) Despawn(clone.gameObject);
		}
		
		// This allows you to despawn a clone via GameObject, with optional delay
		public static void Despawn(GameObject clone, float delay = 0.0f)
		{
			if (clone != null)
			{
				var pool = default(LeanPool);
				
				// Try and find the pool associated with this clone
				if (AllLinks.TryGetValue(clone, out pool) == true)
				{
					// Remove the association
					AllLinks.Remove(clone);
					
					// Despawn it
					pool.FastDespawn(clone, delay);
				}
				else
				{
					//Debug.LogError("Attempting to despawn " + clone.name + ", but failed to find pool for it! Make sure you created it using LeanPool.Spawn!");
					
					// Fall back to normal destroying
					Destroy(clone);
				}
			}
			else
			{
				//Debug.LogError("Attempting to despawn a null clone");
			}
		}
		
		// Returns the total amount of spawned clones
		public int Total
		{
			get
			{
				return total;
			}
		}
		
		// Returns the amount of cached clones
		public int Cached
		{
			get
			{
				return cache.Count;
			}
		}
		
		// This will return a clone from the cache, or create a new instance
		public GameObject FastSpawn(Vector3 position, Quaternion rotation, Transform parent = null)
		{
			if (Prefab != null)
			{
				// Attempt to spawn from the cache
				while (cache.Count > 0)
				{
					// Get last cache entry
					var index = cache.Count - 1;
					var clone = cache[index];
					
					// Remove cache entry
					cache.RemoveAt(index);
					
					if (clone != null)
					{
						// Update transform of clone
						var cloneTransform = clone.transform;
						
						cloneTransform.localPosition = position;
						
						cloneTransform.localRotation = rotation;
						
						cloneTransform.SetParent(parent, false);
						
						// Activate clone
						clone.SetActive(true);
						
						// Messages?
						SendNotification(clone, "OnSpawn");
						
						return clone;
					}
					else
					{
						Debug.LogError("The " + name + " pool contained a null cache entry");
					}
				}
				
				// Make a new clone?
				if (Capacity <= 0 || total < Capacity)
				{
					var clone = FastClone(position, rotation, parent);
					
					// Messages?
					SendNotification(clone, "OnSpawn");
					
					return clone;
				}
			}
			else
			{
				Debug.LogError("Attempting to spawn null");
			}
			
			return null;
		}
		
		// This will despawn a clone and add it to the cache
		public void FastDespawn(GameObject clone, float delay = 0.0f)
		{
			if (clone != null)
			{
				// Delay the despawn?
				if (delay > 0.0f)
				{
					// Make sure we only add it to the marked object list once
					if (delayedDestructions.Exists(m => m.Clone == clone) == false)
					{
						var delayedDestruction = LeanClassPool<DelayedDestruction>.Spawn() ?? new DelayedDestruction();
						
						delayedDestruction.Clone = clone;
						delayedDestruction.Life  = delay;
						
						delayedDestructions.Add(delayedDestruction);
					}
				}
				// Despawn now?
				else
				{
					// Add it to the cache
					cache.Add(clone);
					
					// Messages?
					SendNotification(clone, "OnDespawn");
					
					// Deactivate it
					clone.SetActive(false);
					
					// Move it under this GO
					clone.transform.SetParent(transform, false);
				}
			}
			else
			{
				//Debug.LogWarning("Attempting to despawn a null clone");
			}
		}
		
		// This allows you to make another clone and add it to the cache
		public void FastPreload()
		{
			if (Prefab != null)
			{
				// Create clone
				var clone = FastClone(Vector3.zero, Quaternion.identity, null);
				
				// Add it to the cache
				cache.Add(clone);
				
				// Deactivate it
				clone.SetActive(false);
				
				// Move it under this GO
				clone.transform.SetParent(transform, false);
			}
		}
		
		// Update preloaded count
		protected virtual void Awake()
		{
			UpdatePreload();
		}
		
		// Adds pool to list
		protected virtual void OnEnable()
		{
			AllPools.Add(this);
		}
		
		// Remove pool from list
		protected virtual void OnDisable()
		{
			AllPools.Remove(this);
		}
		
		// Update marked objects
		protected virtual void Update()
		{
			// Go through all marked objects
			for (var i = delayedDestructions.Count - 1; i >= 0; i--)
			{
				var markedObject = delayedDestructions[i];
				
				// Is it still valid?
				if (markedObject.Clone != null)
				{
					// Age it
					markedObject.Life -= Time.deltaTime;
					
					// Dead?
					if (markedObject.Life <= 0.0f)
					{
						RemoveDelayedDestruction(i);
						
						// Despawn it
						FastDespawn(markedObject.Clone);
					}
				}
				else
				{
					RemoveDelayedDestruction(i);
				}
			}
		}
		
		private void RemoveDelayedDestruction(int index)
		{
			var delayedDestruction = delayedDestructions[index];
			
			delayedDestructions.RemoveAt(index);
			
			LeanClassPool<DelayedDestruction>.Despawn(delayedDestruction);
		}
		
		// Makes sure the right amount of prefabs have been preloaded
		private void UpdatePreload()
		{
			if (Prefab != null)
			{
				for (var i = total; i < Preload; i++)
				{
					FastPreload();
				}
			}
		}
		
		// Returns a clone of the prefab and increments the total
		// NOTE: Prefab is assumed to exist
		private GameObject FastClone(Vector3 position, Quaternion rotation, Transform parent)
		{
			var clone = (GameObject)Instantiate(Prefab, position, rotation);
			
			total += 1;
			
			clone.name = Prefab.name + " " + total;
			
			clone.transform.SetParent(parent, false);
			
			return clone;
		}
		
		// Sends messages to clones
		// NOTE: clone is assumed to exist
		private void SendNotification(GameObject clone, string messageName)
		{
			switch (Notification)
			{
				case NotificationType.SendMessage:
				{
					clone.SendMessage(messageName, SendMessageOptions.DontRequireReceiver);
				}
				break;
				
				case NotificationType.BroadcastMessage:
				{
					clone.BroadcastMessage(messageName, SendMessageOptions.DontRequireReceiver);
				}
				break;
			}
		}
	}
}