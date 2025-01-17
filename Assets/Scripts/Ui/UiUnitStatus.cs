﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UiUnitStatus : MonoBehaviour {

    // Stats
    private Transform _stats;
    private TextMeshProUGUI _attack;
    private TextMeshProUGUI _attackSpeed;
    private TextMeshProUGUI _movementSpeed;
    private Image _attackIcon;
    private Image _attackSpeedIcon;
    private Image _movementSpeedIcon;

    // Description
    private Transform _description;
    private TextMeshProUGUI _name;

     void Start() {
        // Unit Status 
        _stats = transform.GetChild(0);
        _attack = _stats.GetChild(0).GetChild(1).GetComponent<TextMeshProUGUI>();
        _attackSpeed = _stats.GetChild(1).GetChild(1).GetComponent<TextMeshProUGUI>();
        _movementSpeed = _stats.GetChild(2).GetChild(1).GetComponent<TextMeshProUGUI>();

        _attackIcon = _stats.GetChild(0).GetChild(0).GetComponent<Image>();
        _attackSpeedIcon = _stats.GetChild(1).GetChild(0).GetComponent<Image>();
        _movementSpeedIcon = _stats.GetChild(2).GetChild(0).GetComponent<Image>();

        // Description 
        _description = transform.GetChild(1);
        _name = _description.GetChild(0).GetComponent<TextMeshProUGUI>();
    }

    public void ChangeUnitStatus(GameObject unit) {
        float attack = unit.GetComponent<Weapon>().AttackDamage;
        float attackSpeed = (float) (1.0 / unit.GetComponent<Weapon>().CooldownTime);
        float movementSpeed = unit.GetComponent<Unit>().MoveSpeed;
        string name = unit.GetComponent<Unit>().Name;

        _attack.text = attack.ToString("0");
        _attackSpeed.text = attackSpeed.ToString("0.00");
        _movementSpeed.text = movementSpeed.ToString("0.0");
        _name.text = name;

        _attackIcon.enabled = true;
        _attackSpeedIcon.enabled = true;
        _movementSpeedIcon.enabled = true;
    }

    public void ClearUnitStatus() {
        _attack.text = "";
        _attackSpeed.text = "";
        _movementSpeed.text = "";
        _name.text = "";

        _attackIcon.enabled = false;
        _attackSpeedIcon.enabled = false;
        _movementSpeedIcon.enabled = false;
    }

}
