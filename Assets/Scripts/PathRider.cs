﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathRider : MonoBehaviour {

	public float gravity = .001f;
	public float maxFall = 1.5f;
	public float onPathSpeed = 2f;
	public float offPathSpeed = 2.5f;
	public ParticleSystem startRidePS;

	RidePath currentPath = null;
	Vector3[] currentPathVectors = null;
	LTSpline currentSpline = null;
	Vector3 currentPathVector = Vector3.zero;
	int currentPathVectorIndex = 0;
	Vector3 nextPathVector = Vector3.zero;
	
	bool canRidePath = true;

	float startTime = 1f;
	float startTimer = 0f;
	bool started = false;

	bool onPath;
	Vector2 velocity;
	bool goingRight = true;
	Collider2D cd;
	Rect box;

	// Raycasting
	int numRays = 5;
	int numPathRays = 1;
	RaycastHit2D closestHorzHitInfo;
	RaycastHit2D closestVertHitInfo;
	RaycastHit2D closestPathHitInfo;

	// Use this for initialization
	void Start () {
		cd = GetComponent<BoxCollider2D>();
		velocity = new Vector2(offPathSpeed, 0f);
		TrailRenderer rend = GetComponentInChildren<TrailRenderer>();
		if (rend)
			rend.sortingLayerName = "Character";
	}
	
	void Update() {
		/*startTimer += Time.deltaTime;
		if (startTimer >= startTime && !started) {
			started = true;
			RidePath(currentPathVectors);
		}*/
		started = true;
	}

	/// <summary>
	/// This function is called every fixed framerate frame, if the MonoBehaviour is enabled.
	/// </summary>
	void FixedUpdate() {
		// For convenience
		box = new Rect(
			cd.bounds.min.x,
			cd.bounds.min.y,
			cd.bounds.size.x,
			cd.bounds.size.y
		);

		// Apply gravity
		if (!onPath) {
			velocity = new Vector2(velocity.x, Mathf.Max(velocity.y - gravity, -maxFall));
		} 
		// Ride path
		else {
			// Calculate velocity based on movement along path
			if (currentSpline.ratioAtPoint(nextPathVector) < currentSpline.ratioAtPoint(transform.position)) {
				currentPathVector = nextPathVector;
				currentPathVectorIndex++;
				nextPathVector = currentPathVectors[currentPathVectorIndex + 1];
			}
			velocity = (nextPathVector - currentPathVector).normalized * onPathSpeed;
		}

		// Calculate ray distance (current fall speed) and direction
		//float rayDistance = box.height / 2 + Mathf.Abs(velocity.y * Time.deltaTime);
		// TODO: This can't be right. The 1.5f is just to lengthen it a little to reduce the chance of missing an edge collider.
		float rayDistance = Mathf.Abs(velocity.magnitude * 1.5f * Time.deltaTime);

		// Check for paths
		// Cast the ray to check for paths
		RaycastHit2D[] pathHitInfo = new RaycastHit2D[numPathRays];
		bool pathHit = false;
		float closestPathHit = float.MaxValue;
		int closestPathHitIndex = 0;
		for (int i = 0; i < numPathRays; i++) {
			// Create and cast ray
			//float lerpDistance = (float)i / (float)((numPathRays > 1) ? numPathRays - 1 : numPathRays);
			Vector2 rayOrigin = transform.position; //Vector2.Lerp(minRay, maxRay, lerpDistance);
			Ray2D ray = new Ray2D(rayOrigin, velocity.normalized);
			pathHitInfo[i] = Physics2D.Raycast(rayOrigin, velocity.normalized, rayDistance, 1 << LayerMask.NameToLayer("Path"));
			Debug.DrawRay(ray.origin, ray.direction * rayDistance, Color.cyan, 1f);
			// Check raycast results and keep track of closest path hit
			if (pathHitInfo[i].fraction > 0) {
				pathHit = true;
				if (pathHitInfo[i].fraction < closestPathHit) {
					closestPathHit = pathHitInfo[i].fraction;
					closestPathHitIndex = i;
					closestPathHitInfo = pathHitInfo[i];
				}
			}
		}

		// Check and handle what path we hit
		if (pathHit) {
			RidePath path = closestPathHitInfo.collider.gameObject.GetComponent<RidePath>();
			// If path exists and is drawOrder is greater than current path's, snap to hit point
			if (path && canRidePath)
				if (currentPath == null || (path.drawOrder > currentPath.drawOrder && path != currentPath)) {
					PlaceSelfInPath(closestPathHitInfo.point, path);
				}
		}
		
	}

	/// <summary>
	/// LateUpdate is called every frame, if the Behaviour is enabled.
	/// It is called after all Update functions have been called.
	/// </summary>
	void LateUpdate() {
		// Move rider
		if (started) {
			if (onPath) {
				
			} else {
				if (currentPath == null)
					transform.Translate(velocity * Time.deltaTime);
			}
		}

		if (velocity.x > 0f)
			goingRight = true;
		else if (velocity.x < 0f)
			goingRight = false;
	}

	void PlaceSelfInPath(Vector3 pos, RidePath path) {
		// Find closest point in path
		Vector3[] pathVectors = path.pathVectors;
		float shortestDistance = float.MaxValue;
		int shortest = -1;
		for (int i = 0; i < pathVectors.Length; i++) {
			float distance = Vector3.Distance(pos, pathVectors[i]);
			if (distance < shortestDistance) {
				shortestDistance = distance;
				shortest = i;
			}
		}

		// Get length of partial path (+ 1 as our point of contact will be the starting point)
		int partialPathLength = (pathVectors.Length - shortest) + 1;
		
		// LeanTween requires 4 or more points in a spline path!
		if (partialPathLength > 3) {
			// Ride partial path
			RidePath(path, shortest, pos);
		}
	}

	void RidePath(RidePath ridePath, int start = 0, Vector3? startVector = null) {
		Vector3[] path = new Vector3[0];

		// Trim beginning of vector array if starting index is not 0...
		if (start != 0) {
			int partialPathLength = ridePath.pathVectors.Length - start;
			int startIndex = 0;
			// Change starting vector if provided
			if (startVector != null && startVector.GetType().Equals(typeof(Vector3))) {
				path = new Vector3[partialPathLength + 1];
				path[0] = (Vector3)startVector;
				startIndex = 1;
			} else {
				path = new Vector3[partialPathLength];
			}
			// Assemble path
			for (int i = startIndex; i < partialPathLength + startIndex; i++) {
				path[i] = ridePath.pathVectors[start + i - startIndex];
			}
		} 
		// ...Otherwise, just ride the provided path from the beginning
		else {
			path = ridePath.pathVectors;
		}

		// Ride the path!
		Debug.Log("riding path!");
		for (int i = 0; i < path.Length - 1; i++) {
			Debug.DrawLine(path[i], path[i+1], Color.red, 5.0f);
		}

		onPath = true;
		currentPath = ridePath;
		currentPathVectors = path;
		currentSpline = SplineHelper.CreateSpline(path);
		currentPathVectorIndex = 0;
		currentPathVector = path[currentPathVectorIndex];
		nextPathVector = path[currentPathVectorIndex + 1];
		LeanTween.moveSpline(gameObject, currentSpline, 10f).setSpeed(onPathSpeed).setOnComplete(TryToLeavePath);
		Instantiate(startRidePS, transform.position, Quaternion.identity);
	}

	void TryToLeavePath() {
		Debug.Log("trying to leave");
		if (Mathf.Abs(currentSpline.ratioAtPoint(transform.position) - 1.0f) < 0.001f ) {
			Debug.Log("approved to leave");
			LeavePath();
		} else {
			LeanTween.moveSpline(gameObject, SplineHelper.CreateSpline(currentPathVectors), 10f).setSpeed(onPathSpeed).setOnComplete(TryToLeavePath);
		}
	}

	void LeavePath(bool setVelocity = true) {
		// Set velocity according to last direction of travel on path
		Debug.Log(currentPathVectors.Length);
		int last = currentPathVectors.Length - 1;
		Vector2 direction = (currentPathVectors[last] - currentPathVectors[last - 1]).normalized;
		if (setVelocity)
			velocity = direction * offPathSpeed;

		// Exit from end of path
		Debug.Log("exiting path!");
		LeanTween.cancel(gameObject);
		onPath = false;
		currentPath = null;
		currentPathVectors = null;
		currentSpline = null;
	}

	/// <summary>
	/// Sent when another object enters a trigger collider attached to this
	/// object (2D physics only).
	/// </summary>
	/// <param name="other">The other Collider2D involved in this collision.</param>
	void OnTriggerEnter2D(Collider2D other) {
		if (other.gameObject.layer == LayerMask.NameToLayer("Consumable")) {
			Consumable consumable = other.gameObject.GetComponent<Consumable>();
			Debug.Log(consumable.restorePercentResource);
			consumable.gameObject.SetActive(false);
		}
		else if (other.gameObject.layer == LayerMask.NameToLayer("Obstacle")) {
			Bumper bumper = other.gameObject.GetComponent<Bumper>();
			if (bumper)
				Bumper(bumper);
		}
	}

	void Bumper(Bumper bumper) {
		Vector3 diff =  transform.position - bumper.transform.position;
		velocity = diff.normalized * bumper.knockback;
		StartCoroutine(DisablePathRiding(0.5f));
		
		if (bumper.durability == 0)
			bumper.gameObject.SetActive(false);
		else
			bumper.durability--;
	}

	IEnumerator DisablePathRiding(float time) {
		if (onPath)
			LeavePath(false);
		yield return StartCoroutine(CannotRidePath(time));
		canRidePath = true;
	}

	IEnumerator CannotRidePath(float time) {
		float timer = 0f;
		while (timer < time) {
			timer += Time.deltaTime;
			canRidePath = false;
			yield return null;
		}
	}

	/// <summary>
	/// OnGUI is called for rendering and handling GUI events.
	/// This function can be called multiple times per frame (one call per event).
	/// </summary>
	void OnGUI() {
		GUI.Box(new Rect(5,5,150,30), "velocity: " + velocity);
		GUI.Box(new Rect(5,40,50,30), canRidePath.ToString());
	}
}
