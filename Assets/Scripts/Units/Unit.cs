﻿using Pathfinding;
using Pathfinding.RVO;
using System.Collections.Generic;
using UnityEngine;
using System;
using PathCreation;

//[RequireComponent(typeof(Seeker))]
//[RequireComponent(typeof(RVOController))]
[RequireComponent(typeof(UnitAlignmentIndicator))]
[RequireComponent(typeof(Armor))]
public class Unit : MonoBehaviour {
    internal class Command {
        internal Unit _unit;
        internal Vector3 _dest;
        internal MovementMode _mode;

        internal Command(MovementMode mode, Unit transform) {
            _mode = mode;
            _unit = transform;
        }

        internal Command(MovementMode mode, Vector3 dest) {
            _mode = mode;
            _dest = dest;
        }
    }

    public Alignment Alignment;
    public bool IsControllable;

    // portrait stats

    public float ThreatLevel = 1;
    public string Name;
    public float MaxHealth;
    [HideInInspector]
    public float Health {
        get;
        private set;
    }
    public bool DestroyImmediately;
    public GameObject Deathrattle;

    // unit stats
    [HideInInspector]
    public float RetargetTime = 6.0f;
    public float DetectionRange;
    public float AMoveStopDistMultiplier = 1f;
    public float LoseVisionMultiplier = 1.1f;
    public float MoveSpeed = 10f;
    public float OnEnemyDeathReward = 1f;

    // in degrees
    public float MaxRotatingSpeed = 360;

    public Vector3 selectRingSize = new Vector3(0.2f, 0.2f, 1f);
    public GameObject EffectSpawningPoint;
    GameObject _healthBar;
    UiOverlayManager _uiOverlayManager;

    bool _movementLocked;

    Animator _animRef;
    IAstarAI _aiRef;
    Queue<Command> _shiftQueue = new Queue<Command>();
    Vector3 _guardPosition;
    RVOController _rvoRef;
    RTSAvoidance _avoidanceRef;
    protected MovementMode _movementMode = MovementMode.AMove;
    protected Stance _stance = Stance.DefensiveStance;
    SphereCollider _rangeCollider;
    Weapon _weaponRef;
    Armor _armorRef;
    UnitAlignmentIndicator _light;

    bool _decompose = false;

    Unit _focusTarget;
    bool _attacking = false;
    Unit _following;

    HashSet<Unit> _targets = new HashSet<Unit>();

    bool _startHasRun = false;
    protected Action DeathCallback = () => {};

    // Select Ring
    public GameObject selectRingPrefab;
    GameObject _selectRing;

    private void Awake() {
        RTSUnitManager.Instance.AddUnit(gameObject);
    }

    public void DisableMovement(bool move) {
        _movementLocked = move;
        ExecuteFirstQueueAction();
    }
    public void AnimatorStartMoving() {
        if (_animRef != null) {
            _animRef.SetBool("IsMoving", true);
        }
    }
    public void AnimatorStopMoving() {
        if (_animRef != null) {
            _animRef.SetBool("IsMoving", false);
        }
    }
    public void AnimatorStartFiring() {
        if (_animRef != null) {
            _animRef.SetBool("IsFiring", true);
        }
    }
    public void AnimatorStopFiring() {
        if (_animRef != null) {
            _animRef.SetBool("IsFiring", false);
        }
    }

    public void Stop() {
        _shiftQueue.Clear();
        NextDestination();
        SetDestination(transform.position);
        _aiRef.SearchPath();
        StartIdle();
    }

    public void StartIdle() {
        _guardPosition = transform.position;
        this._movementMode = MovementMode.Guard;
        this._stance = Stance.DefensiveStance;
        AnimatorStopMoving();
    }

    public void HoldGround() {
        Stop();
        this._stance = Stance.HoldGround;
    }

    public void NextDestination() {
        // dont do anything if attack or following alive unit
        if (_attacking || (_movementMode == MovementMode.Follow && _following != null) || _shiftQueue.Count == 0 || _movementLocked) {
            return;
        }

        _guardPosition = new Vector3(float.PositiveInfinity, 0, 0);
        _following = null;

        if (_shiftQueue.Count > 0) {
            _shiftQueue.Dequeue();
        }
        if (_shiftQueue.Count == 0) {
            StartIdle();
            return;
        }
        ExecuteFirstQueueAction();
    }

    public void ShiftMove(Vector3 moveTo, MovementMode mode) {
        _attacking = false;
        AnimatorStopFiring();
        _guardPosition = new Vector3(float.PositiveInfinity, 0, 0);
        _shiftQueue.Enqueue(new Command(mode, moveTo));
        ExecuteFirstQueueAction();
    }

    public int ShiftMove(RaycastHit hit, MovementMode mode) {
        _attacking = false;
        AnimatorStopFiring();
        _guardPosition = new Vector3(float.PositiveInfinity, 0, 0);
        Unit temp = hit.transform.GetComponentInParent<Unit>();
        // if raycast hit ground instead of unit
        if (temp == null) {
            ShiftMove(hit.point, mode);
            return mode == MovementMode.AMove ? 1 : 0;
        }

        // follow target
        if (temp.Alignment == Alignment.Friendly) {
            ShiftFollow(temp);
            return 3;
        } else { // attack target
            ShiftAttack(temp);
            return 2;
        }
    }

    public void Move(Vector3 moveTo, MovementMode mode) {
        _shiftQueue.Clear();
        ShiftMove(moveTo, mode);
    }

    public int Move(RaycastHit hit, MovementMode mode) {
        _shiftQueue.Clear();
        return ShiftMove(hit, mode);
    }
    
    public void ShiftFollow(Unit u) {
        _guardPosition = new Vector3(float.PositiveInfinity, 0, 0);
        _shiftQueue.Enqueue(new Command(MovementMode.Follow, u));
        if (_shiftQueue.Count == 1) {
            ExecuteFirstQueueAction();
        }
    }

    public void Follow(Unit u) {
        _shiftQueue.Clear();
        ShiftFollow(u);
    }

    public void ShiftAttack(Unit u) {
        _guardPosition = new Vector3(float.PositiveInfinity, 0, 0);
        _shiftQueue.Enqueue(new Command(MovementMode.Attack, u));
        if (_shiftQueue.Count == 1) {
            ExecuteFirstQueueAction();
        }
    }

    public void Attack(Unit u) {
        _shiftQueue.Clear();
        ShiftAttack(u);
    }

    Vector3 SetDestination(Vector3 dest) {
        if (_movementLocked) {
            return transform.position;
        }
        return _aiRef.destination = dest;
    }

    void ExecuteFirstQueueAction() {
        if (!IsAlive() || _shiftQueue.Count == 0) {
            return;
        }

        _guardPosition = new Vector3(float.PositiveInfinity, 0, 0);
        var temp = _shiftQueue.Peek();
        _movementMode = temp._mode;
        Vector3 dest = transform.position;
        switch (temp._mode) {
            case MovementMode.AMove:
                this._stance = Stance.DefensiveStance;
                dest = SetDestination(temp._dest);
                break;
            case MovementMode.Move:
                this._stance = Stance.HoldFire;
                dest = SetDestination(temp._dest);
                break;
            case MovementMode.Attack:
                if (temp._unit != null) {
                    _following = temp._unit;
                    _focusTarget = temp._unit;
                    dest = SetDestination(temp._unit.transform.position);
                    _aiRef.SearchPath();
                } else {
                    NextDestination();
                    break;
                }
                break;
            case MovementMode.Follow:
                if (temp._unit != null) {
                    _following = temp._unit;
                } else {
                    NextDestination();
                    break;
                }
                break;
            default:
                break;
        }

        // only search if not in range
        if (_movementMode == MovementMode.Move || Vector3.Distance(dest, transform.position) >= _weaponRef.AttackRange) {
            AnimatorStartMoving();
            _aiRef.SearchPath();
        }
    }

    public bool IsAlive() {
        return Health > 0;
    }
    
    public void Start()
    {
        if (_startHasRun) {
            return;
        }

        Health = MaxHealth;

        _animRef = GetComponentInChildren<Animator>();

        if (_animRef != null) {
            _animRef.SetFloat("RandomOffset", UnityEngine.Random.Range(0f, 1f));
        }

        if (TryGetComponent<IAstarAI>(out _aiRef)) {
            _aiRef.maxSpeed = MoveSpeed;
            ((RichAI)_aiRef).rotationSpeed = MaxRotatingSpeed;
        }
        if (TryGetComponent<RVOController>(out _rvoRef)) {
            _rvoRef.StartMoving += AnimatorStartMoving;
            _rvoRef.StopMoving += AnimatorStopMoving;
        }
        if (TryGetComponent<RTSAvoidance>(out _avoidanceRef)) {
            _avoidanceRef.ReachedDestinationCallback += NextDestination;
        }
        
        _rangeCollider = gameObject.AddComponent<SphereCollider>();
        _rangeCollider.radius = DetectionRange;
        _rangeCollider.isTrigger = true;

        if (TryGetComponent<Weapon>(out _weaponRef)) {
            _weaponRef.RotationSpeed = _weaponRef.CanMoveWhileAttacking ? _weaponRef.RotationSpeed : MaxRotatingSpeed;
        }
        _armorRef = GetComponent<Armor>();
        _light = GetComponent<UnitAlignmentIndicator>();

        Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;

        _uiOverlayManager = UiToGameManager.Instance.GetUiOverlayManager();
        _healthBar = _uiOverlayManager.CreateUnitHealthBar(Health, MaxHealth, this.Alignment);


        if (this.Alignment == Alignment.Friendly)
        {
            DeathCallback = () => {
                RTSUnitManager.Instance.ReportUnitDead(gameObject);
            };
        }
        else
        {
            DeathCallback = () => {
                ResourceManager.Instance.IncreaseRamen(OnEnemyDeathReward);
            };
        }

        if (selectRingPrefab != null) {
            _selectRing = Instantiate(selectRingPrefab, gameObject.transform);
            _selectRing.transform.localScale = selectRingSize;
        }

        if (_aiRef != null) {
            Move(transform.position, MovementMode.Move);
        }

        _startHasRun = true;
    }

    void TargetThisLater(Unit u) {
        ShiftFollow(u);
    }

    bool GetBestTarget() {
        float bestTimeToKill = float.PositiveInfinity;
        var highestThreatLevel = 0f;
        _targets.RemoveWhere(u => !u.IsAlive());
        _targets.RemoveWhere(u => u == null);
        foreach (var unit in _targets) {
            var dist = DistanceIgnoreY(unit.transform.position, transform.position);
            var effectiveness = unit.GetComponent<Armor>().ReduceDamage(new DamageMetadata(1.0f, _weaponRef.DamageType));
            var dpsToUnit = effectiveness * _weaponRef.DPS;
            var timeToKill = unit.Health / dpsToUnit;
            var localRange = _stance == Stance.HoldGround ? _weaponRef.AttackRange : DetectionRange;

            if (effectiveness <= 0.04f) {
                continue;
            }
            if (ThreatLevel <= 0) {
                continue;
            }

            if (unit.ThreatLevel > highestThreatLevel && dist < localRange) {
                highestThreatLevel = unit.ThreatLevel;
                _focusTarget = unit;
                bestTimeToKill = timeToKill;
            }

            // if both can one shot
            if (timeToKill < _weaponRef.CooldownTime && bestTimeToKill < _weaponRef.CooldownTime && dist < localRange) {
                // select the one with higher kill time
                if (timeToKill > bestTimeToKill) {
                    if (_shiftQueue.Count <= 0 && typeof(Truck).IsInstanceOfType(unit) && _aiRef != null) {
                        TargetThisLater(unit);
                    }
                    _focusTarget = unit;
                    bestTimeToKill = timeToKill;
                }
            } else { // cannot one shot, so select the one that takes less shits
                if (timeToKill < bestTimeToKill && dist < localRange) {
                    if (_shiftQueue.Count <= 0 && typeof(Truck).IsInstanceOfType(unit) && _aiRef != null) {
                        TargetThisLater(unit);
                    }
                    _focusTarget = unit;
                    bestTimeToKill = timeToKill;
                }
            }
        }
        return _focusTarget != null;
    }

    void ReacquireTarget() {
        // dont override player command
        if (_movementMode == MovementMode.Attack) {
            return;
        }

        if (_focusTarget != null) {
            GetBestTarget();
            CancelInvoke("ReacquireTarget");
            Invoke("ReacquireTarget", RetargetTime);
        }
    }

    void AcquireTarget() {
        if (_focusTarget == null) {
            if (GetBestTarget()) {
                Invoke("ReacquireTarget", RetargetTime);
            }
        }
    }

    void MobileAttackLogic() {
        // cannot attack if on Move and cannot move while attacking
        if (_movementMode == MovementMode.Move && !_weaponRef.CanMoveWhileAttacking) {
            return;
        }

        AcquireTarget();

        // if have target
        if (_focusTarget != null) {
            var distanceToFocus = DistanceIgnoreY(_focusTarget.transform.position, transform.position);

            // move to target if outside attack range
            if (distanceToFocus > _weaponRef.AttackRange) {
                // move only if cannot attack while moving
                if (_movementMode != MovementMode.Move) {
                    SetDestination(_focusTarget.transform.position);
                    _aiRef.SearchPath();
                    AnimatorStopFiring();
                    AnimatorStartMoving();
                }

                if (_weaponRef.CanMoveWhileAttacking) {
                    _weaponRef.ContinueToAimAt(_focusTarget);
                }
            } else {
                // attack if inside range
                if (_weaponRef.AimAt(_focusTarget)) {
                    AnimatorStartFiring();
                }

                // move to mult distance AMoveStopDistMultiplier
                if (!_weaponRef.CanMoveWhileAttacking || distanceToFocus <= _weaponRef.AttackRange * AMoveStopDistMultiplier) {
                    // lock only if cannot attack while moving
                    if (_movementMode != MovementMode.Move) {
                        _attacking = true;
                        SetDestination(transform.position);
                        _aiRef.SearchPath();
                        AnimatorStopMoving();
                    }
                }
            }
        } else {
            if (_weaponRef.CanMoveWhileAttacking) { // no enemy found
                                                    // rotate turret back to forwards
                _weaponRef.ResetRotation(transform.forward);
            }
            AnimatorStopFiring();
            _attacking = false;

            // if no target move to guar pos
            if (!float.IsPositiveInfinity(_guardPosition.x) && Vector3.Distance(_guardPosition, transform.position) < Vector3.kEpsilon && _aiRef.destination != _guardPosition) {
                Move(_guardPosition, MovementMode.AMove);
            } else if (_shiftQueue.Count == 0) {
                // already at guard position
                // do nothing
            } else if (_movementMode == MovementMode.AMove && _aiRef.destination != _shiftQueue.Peek()._dest) { // if Amoving somewhere continue moving to dest
                ExecuteFirstQueueAction();
            }

        }
    }

    void StationaryAttackLogic() {
        AcquireTarget();

        if (_focusTarget != null) {
            var distanceToFocus = DistanceIgnoreY(_focusTarget.transform.position, transform.position);

            if (distanceToFocus <= _weaponRef.AttackRange) {
                // attack if inside range
                _weaponRef.AimAt(_focusTarget);
            }
        }
    }

    void AttackingLogic() {
        // cannot attack
        if (_weaponRef == null) {
            return;
        }

        // check if lost vision or target is dead
        if (_focusTarget != null) {
            if (!_focusTarget.IsAlive()
                || (_stance == Stance.HoldGround && DistanceIgnoreY(_focusTarget.transform.position, transform.position) > _weaponRef.AttackRange) // hold ground behavior
                || DistanceIgnoreY(_focusTarget.transform.position, transform.position) > DetectionRange * (_aiRef != null ? LoseVisionMultiplier : 1)) { // normal behavior
                _focusTarget = null;
                _weaponRef.LoseAim();
                _attacking = false;
                AnimatorStopFiring();
            }
        }

        // explicit attack target
        if (_movementMode == MovementMode.Attack && _following != null) {
            _focusTarget = _following;
        }

        // if can pathfind
        if (_aiRef != null && !_movementLocked && _stance != Stance.HoldGround) {

            MobileAttackLogic();

        } else { // possibly a turret or maybe a truck?

            StationaryAttackLogic();

        }
    }
    
    protected void Update() {
        // dont do anything if dead
        if (!IsAlive()) {
            if (_decompose) {
                var p = transform.position;
                p.y -= 1 * Time.deltaTime;
                transform.position = p;
            }
            return;
        }

        if (_movementMode == MovementMode.Follow || _movementMode == MovementMode.Attack) {
            if (_following != null && _following.IsAlive()) {
                if (_movementMode == MovementMode.Follow) {
                    var dist = DistanceIgnoreY(transform.position, _following.transform.position);
                    var radius = _following.GetComponent<RichAI>().radius;
                    if (dist <= 2f * radius) {
                        SetDestination(transform.position);
                        AnimatorStopMoving();
                    } else if(dist >= 3f * radius) {
                        SetDestination(_following.transform.position);
                        if (!_following.IsRVOLocked()) {
                            AnimatorStartMoving();
                        }
                    }
                } else if (_movementMode == MovementMode.Attack){
                    SetDestination(_following.transform.position);
                }
            } else {
                // target is dead
                _following = null;
                NextDestination();
            }
        }

        // BELOW IS ATTACKING LOGIC
        AttackingLogic();

        // Update health bar
        _uiOverlayManager.UpdateUnitHealthBar(_healthBar, transform.position, Health, MaxHealth);
    }

    public void TakeDamage(DamageMetadata dm) {
        // to prevent double deaths
        if (Health <= 0) {
            return;
        }

        this.Health -= _armorRef.ReduceDamage(dm);

        if (Health <= 0) {
            Die();
        }
    }

    void Die() {
        // Play death animation etc etc
        if (_aiRef != null) {
            Destroy(((RichAI)_aiRef));
        }
        if (_rvoRef != null) {
            Destroy(_rvoRef);
        }
        if (_animRef != null) {
            _animRef.SetTrigger("Die");
        }
        _light.Destroy();
        RTSUnitManager.Instance.RemoveUnit(gameObject);

        if (TryGetComponent<UnitSoundController>(out UnitSoundController usc)) {
            usc.Die();
        }

        DeathCallback();
        Destroy(_healthBar);

        // todo refactor into something nice
        Destroy(GetComponentInChildren<CapsuleCollider>());
        Destroy(GetComponentInChildren<BoxCollider>());
        if (TryGetComponent<NavmeshCut>(out var navmeshCut)) {
            Destroy(navmeshCut);
        }
        if (Deathrattle != null) {
            Instantiate(Deathrattle, transform.position, transform.rotation);
        }
        if (DestroyImmediately) {
            var arr = GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer cmr in arr) {
                Destroy(cmr);
            }
            var arr2 = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer csmr in arr2) {
                Destroy(csmr);
            }
            Invoke("Destroy", 10);
            return;
        }
        Invoke("Sink", 2);
        Invoke("Destroy", 8);
    }

    void Sink() {
        _decompose = true;
    }

    void Destroy() {
        Destroy(gameObject);
    }

    float DistanceIgnoreY(Vector3 a, Vector3 b) {
        a.y = 0;
        b.y = 0;
        return Vector3.Distance(a, b);
    }

    void OnTriggerEnter(Collider other) {
        Unit u = other.GetComponent<Unit>();

        if (u == null) {
            return;
        }

        if (u.Alignment != this.Alignment) {
            _targets.Add(u);
        }
    }

    void OnTriggerExit(Collider other) {
        Unit u = other.GetComponent<Unit>();

        if (u == null) {
            return;
        }
        _targets.Remove(u);
    }

    public void ActivateSelectRing() {
        if (_selectRing != null) {
            _selectRing.SetActive(true);
        }
    }

    public void DeactivateSelectRing() {
        if (_selectRing != null) {
            _selectRing.SetActive(false);
        }
    }

    public GameObject GetHealthBar() {
        return _healthBar;
    }

    public void LockMovement(bool locked) {
        _rvoRef.locked = locked;
    }

    public bool IsRVOLocked() {
        return _rvoRef.locked;
    }
}
