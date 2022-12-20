using UnityEngine;
using Assets.MultiAudioListener;
public class Projectile : MonoBehaviour
{
	public GameObject owner;
	public GameObject fx;
	[HideInInspector]
	public GameObject cgameObject;
	public string projectileName;
	public bool destroyAfterUse = true;
	public float speed = 4f;
	public int damageMin = 3;
	public int damageMax = 24;
	public int blastDamage = 0;
	public float projectileRadius = .2f;
	public float explosionRadius = 2f;
	public DamageType damageType = DamageType.Generic;
	public float pushForce = 0f;
	public GameObject OnDeathSpawn;
	public GameObject SecondaryOnDeathSpawn;
	public string _onFlySound;
	public string _onDeathSound;

	[HideInInspector]
	public MultiAudioSource audioSource;

	//Needed for homing projectires
	public GameObject target = null;
	const float capAngle = 16.875f;

	[HideInInspector]
	public bool goingUp = false;

	RaycastHit[] hits = new RaycastHit[10];

	public float _lifeTime = 1;
	float time = 0f;
	void Awake()
	{
		cgameObject = gameObject;
		if (!string.IsNullOrEmpty(_onFlySound))
		{
			audioSource = GetComponent<MultiAudioSource>();
			if (audioSource == null)
			{
				audioSource = cgameObject.AddComponent<MultiAudioSource>();
				//audioSource.minDistance = 5f;
				audioSource.PlayOnAwake = false;
			}
			audioSource.Loop = true;
			audioSource.AudioClip = SoundLoader.LoadSound(_onFlySound);
			audioSource.Play();
		}
		if (OnDeathSpawn != null)
		{
			if (!PoolManager.HasObjectPool(OnDeathSpawn.name))
				PoolManager.CreateObjectPool(OnDeathSpawn.name, OnDeathSpawn, 10);
		}
		if (SecondaryOnDeathSpawn != null)
		{
			if (!PoolManager.HasObjectPool(SecondaryOnDeathSpawn.name))
				PoolManager.CreateObjectPool(SecondaryOnDeathSpawn.name, SecondaryOnDeathSpawn, 20);
		}
	}

	void OnEnable()
	{
		//Reset Timer
		time = 0f;
	}

	void OnDisable()
	{

	}
	void Update()
	{
		if (GameManager.Paused)
			return;

		time += Time.deltaTime;

		if (time >= _lifeTime)
		{
			//Reset Timer
			time = 0f;
			cgameObject.SetActive(false);
			if (OnDeathSpawn != null)
			{
				GameObject go = PoolManager.GetObjectFromPool(OnDeathSpawn.name);
				go.transform.position = transform.position - transform.forward * .2f;
			}
			return;
		}
		//check for collision
		float nearest = float.MaxValue;
		{
			Vector3 dir = transform.forward;
			int max = Physics.SphereCastNonAlloc(transform.position, projectileRadius, dir, hits, speed * Time.deltaTime, ~((1 << GameManager.InvisibleBlockerLayer) | (1 << GameManager.RagdollLayer)), QueryTriggerInteraction.Ignore);

			if (max > hits.Length)
				max = hits.Length;

			for (int i = 0; i < max; i++)
			{
				RaycastHit hit = hits[i];
				if (hit.collider.gameObject == owner)
					continue;

				if (hit.distance < nearest)
					nearest = hit.distance;

				if ((damageType == DamageType.Rocket) || (damageType == DamageType.Grenade) ||(damageType == DamageType.Plasma) || (damageType == DamageType.BFGBall))
				{
					Vector3 impulseDir = dir.normalized;

					Damageable d = hit.collider.GetComponent<Damageable>();
					if (d != null)
					{
						switch (damageType)
						{
							case DamageType.BFGBall:
								d.Damage(Random.Range(damageMin, damageMax + 1) * 100, damageType, owner);
								d.Impulse(impulseDir, pushForce);
								break;
							default:
								d.Damage(Random.Range(damageMin, damageMax + 1), damageType, owner);
								d.Impulse(impulseDir, pushForce);
								break;
						}
					}
				}
			}
		}

		//explosion
		if (nearest < float.MaxValue)
		{
			transform.position = transform.position + transform.forward * nearest;

			Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, ~(1 << GameManager.InvisibleBlockerLayer), QueryTriggerInteraction.Ignore);
			foreach (Collider hit in hits)
			{
				float distance;
				Damageable d = hit.GetComponent<Damageable>();
				if (d != null)
				{
					Vector3 impulseDir = (hit.transform.position - transform.position).normalized;

					switch (damageType)
					{
						case DamageType.Explosion:
						case DamageType.Rocket:
							distance = (hit.transform.position - transform.position).magnitude;
							d.Damage(Mathf.CeilToInt(Mathf.Lerp(blastDamage, 1, distance / explosionRadius)), DamageType.Explosion, owner);
							d.Impulse(impulseDir, Mathf.Lerp(pushForce, 100, distance / explosionRadius));
							break;
						case DamageType.Plasma:
							if (hit.gameObject == owner) //Plasma never does self damage
								continue;
							else
								d.Damage(Random.Range(damageMin, damageMax + 1), damageType, owner);
							break;
						case DamageType.BFGBall:
							if (hit.gameObject == owner) //BFG never does self damage
								continue;
							else
								d.Damage(Random.Range(damageMin, damageMax + 1) * 100, damageType, owner);
							break;
						case DamageType.Telefrag:
							distance = (hit.transform.position - transform.position).magnitude;
							d.Damage(blastDamage, DamageType.Telefrag, owner);
							d.Impulse(impulseDir, Mathf.Lerp(pushForce, 100, distance / explosionRadius));
							break;
						default:
							d.Damage(Random.Range(damageMin, damageMax + 1), damageType, owner);
							break;
					}
				}
			}

			if (OnDeathSpawn != null)
			{
				GameObject go = PoolManager.GetObjectFromPool(OnDeathSpawn.name);
				go.transform.position = transform.position - transform.forward * .2f;
			}

			if (damageType == DamageType.BFGBall)
			{
				PlayerInfo playerInfo = owner.GetComponent<PlayerInfo>();
				if (playerInfo != null)
				{
					Camera rayCaster = playerInfo.playerCamera.SkyholeCamera;
					Quaternion Camerarotation;
					RaycastHit[] hitRays = new RaycastHit[3];
					Ray r;
					int numRay = 0;
					int index = 0;
					Vector3 dir = transform.forward;
					dir.y = 0;
					Camerarotation = rayCaster.transform.rotation;
					rayCaster.transform.rotation = Quaternion.LookRotation(dir);
					for (int k = 0; (k <= BFGTracers.samples) && (numRay <= 40); k++)
					{
						r = rayCaster.ViewportPointToRay(new Vector3(BFGTracers.hx[index], BFGTracers.hy[index], 0f));
						index++;
						if (index >= BFGTracers.pixels)
							index = 0;
						int max = Physics.RaycastNonAlloc(r, hitRays, 300, ~((1 << GameManager.InvisibleBlockerLayer) |
																		   (1 << GameManager.ThingsLayer) |
																		   (1 << GameManager.CombinesThingsLayer) |
																		   (1 << GameManager.ThingsPlayer1Layer) |
																		   (1 << GameManager.ThingsPlayer2Layer) |
																		   (1 << GameManager.ThingsPlayer3Layer) |
																		   (1 << GameManager.ThingsPlayer4Layer) |
																		   (1 << GameManager.RagdollLayer)), QueryTriggerInteraction.Ignore);
						if (max > hitRays.Length)
							max = hitRays.Length;
						for (int i = 0; i < max; i++)
						{
							GameObject hit = hitRays[i].collider.gameObject;
							Damageable d = hit.GetComponent<Damageable>();
							if (d != null)
							{
								if (hit == owner)
									continue;

								while ((d.Dead == false) && (numRay <= 40))
								{
									d.Damage(Random.Range(49, 88), DamageType.BFGBlast, owner);
									d.Impulse(r.direction, pushForce);
									numRay++;
								}
								GameObject go = PoolManager.GetObjectFromPool(SecondaryOnDeathSpawn.name);
								go.transform.position = hit.transform.position + hit.transform.up * 0.5f;
							}
						}
						/*						
						GameObject visualLine = new GameObject();
						LineRenderer lr = visualLine.AddComponent<LineRenderer>();
						lr.positionCount = 2;
						lr.startColor = Color.green;
						lr.endColor = Color.green;
						lr.SetPosition(0, r.origin);
						lr.SetPosition(1, r.origin + r.direction * 200);
						lr.widthMultiplier = .02f;
						DestroyAfterTime dat = visualLine.AddComponent<DestroyAfterTime>();
						dat._lifeTime = 5;*/
					}
					rayCaster.transform.rotation = Camerarotation;
				}
			}

			if (!string.IsNullOrEmpty(_onDeathSound))
				AudioManager.Create3DSound(transform.position, _onDeathSound, 5f);
			if (destroyAfterUse)
				DestroyAfterTime.DestroyObject(cgameObject);
			else
				cgameObject.SetActive(false);
			return;
		}

		if (target != null)
		{
			Vector3 aimAt = (target.transform.position - transform.position).normalized;
			float angle = Vector3.SignedAngle(aimAt, transform.forward, transform.up);
			if (Mathf.Abs(angle) > capAngle)
			{
				Quaternion newRot;
				if (angle > 0)
					newRot = Quaternion.AngleAxis(capAngle, transform.up);
				else
					newRot = Quaternion.AngleAxis(-capAngle, transform.up);
				aimAt = (newRot * transform.forward).normalized;
			}
			transform.forward = aimAt;
		}

		if (goingUp)
			transform.position = transform.position + transform.up * speed * Time.deltaTime;
		else
			transform.position = transform.position + transform.forward * speed * Time.deltaTime;
	}
}

public static class BFGTracers
{
	public static float[] hx;
	public static float[] hy;
	public static int pixels;
	public static int samples = 500;
	private static float HaltonSequence(int index, int b)
	{
		float r = 0.0f;
		float f = 1.0f / b;
		int i = index;

		while (i > 0)
		{
			r = r + f * (i % b);
			i = Mathf.FloorToInt(i / b);
			f = f / b;
		}

		return r;
	}

	public static void SetTracers()
	{
		pixels = Mathf.FloorToInt(Screen.currentResolution.width * Screen.currentResolution.height / 4f);
		hx = new float[pixels];
		hy = new float[pixels];

		for (int i = 0; i < pixels; i++)
		{
			hx[i] = HaltonSequence(i, 2);
			hy[i] = HaltonSequence(i, 3);
		}
	}
}