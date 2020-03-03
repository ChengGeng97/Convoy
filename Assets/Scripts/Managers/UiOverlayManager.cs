﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

public class UiOverlayManager : Manager<UiOverlayManager> {

    // Serialized Prefabs
    [SerializeField] 
    private GameObject _healthBarPrefab = null;
    [SerializeField]
    private GameObject _popUpPrefab = null;
    [SerializeField]
    private GameObject _resourceGainPopupPrefab = null;
    [SerializeField]
    private GameObject _resourceLossPopupPrefab = null;
    
    // UI In Game Canvas
    private GameObject _uiInGameCanvas = null;
    private GameObject _healthBarPanel = null;

    // UI Inteface Canvas
    private GameObject _uiInterfaceCanvas = null;
    private GameObject _minimap = null;
    private GameObject _deployedUnitsPanel = null;
    private GameObject _trainUnitsPanel = null;
    private GameObject _trainingQueue = null;
    private GameObject _popUpPanel = null;
    
    private UiUnitStatus _uiUnitStatus = null;
    private TextMeshProUGUI _resourcesText = null;
    private GameObject _resourceChange = null;
    private DeployedUnitDictionary _deployedUnitDictionary = null;
    
    // Others
    private Camera _playerCamera = null;
    private Camera _minimapCamera = null;
    private Vector3 _mousePos;

    private bool _isDragging = false;

    void Start() {
        // UI In Game Canvas
        _uiInGameCanvas = GameObject.Find("UiInGameCanvas");
        _healthBarPanel = GameObject.Find("HealthBarPanel");

        // UI Interface Canvas
        _uiInterfaceCanvas = GameObject.Find("UiInterfaceCanvas");
        _minimap = GameObject.Find("Minimap");
        _deployedUnitsPanel = GameObject.Find("DeployedUnitsPanel");
        _trainUnitsPanel = GameObject.Find("TrainUnitsPanel");
        _resourcesText = GameObject.Find("ResourcesText").GetComponent<TextMeshProUGUI>();
        _resourceChange = GameObject.Find("ResourceChange");
        _trainingQueue = GameObject.Find("TrainingQueue");
        _uiUnitStatus = GameObject.Find("UnitStatus").GetComponent<UiUnitStatus>();
        _popUpPanel = _uiInterfaceCanvas.transform.GetChild(6).gameObject;
        _deployedUnitDictionary = GetComponent<DeployedUnitDictionary>();

        // Others
        _playerCamera = Camera.main;
        _minimapCamera = GameObject.Find("MinimapCamera").GetComponent<Camera>();

        // Startup
        GetTrainingUnitsPanelInfo(TrainingUnitsQueueManager.Instance.GetUnitTypeList());
    }

    void Update() {
        UpdateDrawingBox();
    }

    private void OnGUI() {
        if(_isDragging) {
            var rect = ScreenHelper.GetScreenRect(_mousePos, Input.mousePosition);
            ScreenHelper.DrawScreenRectBorder(rect, 1, new Color(0.6f, 0.9608f, 0.706f));
        }

        // var minimapRect = ScreenHelper.GetScreenRect();
        // ScreenHelper.DrawScreenRectBorder(minimapRect, 1, new Color(1f, 1f, 1f));
    }

    /*================ DrawingBox ================*/
    private void UpdateDrawingBox() {
        if(Input.GetMouseButtonDown(0)) {
            if(IsPointerNotOverUI()) {
                _mousePos = Input.mousePosition;
                _isDragging = true;
            }
        }

        if(Input.GetMouseButtonUp(0)) {
            _isDragging = false;
        }

        if(Input.GetMouseButton(0)) {
            if(_minimap.GetComponent<UiMinimap>().GetIsHovering() && !_isDragging) {
                MoveCameraThroughMinimap(Input.mousePosition);
            }
        }
    }

    /*================ Health Bar ================*/
    public GameObject CreateUnitHealthBar(float health, float maxHealth) {
        _healthBarPanel = GameObject.Find("HealthBarPanel");
        GameObject healthBar = Instantiate(_healthBarPrefab, Vector3.zero, Quaternion.identity);
        healthBar.transform.SetParent(_healthBarPanel.transform);
        
        // Change the size of the health bar
        healthBar.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        
        // Change health bar itself
        healthBar.transform.GetChild(0).gameObject.GetComponent<SimpleHealthBar>().UpdateBar(health, maxHealth);

        return healthBar;
    }

    // The bar position should be anchored to the unit or something.
    public void UpdateUnitHealthBar(GameObject healthBar, Vector3 unitPos, float health, float maxHealth) {
        // Transform from world space to canvas space
        if(healthBar != null) {
            unitPos += new Vector3(0, 2.5f, 0);
            _playerCamera.WorldToScreenPoint(unitPos);
            healthBar.transform.position = _playerCamera.WorldToScreenPoint(unitPos);
            healthBar.transform.GetChild(0).gameObject.GetComponent<SimpleHealthBar>().UpdateBar(health, maxHealth);
        }
    }

    /*================ Select Unit ================*/
    public void SelectAllyUnits(List<GameObject> allyList) {
        foreach(Transform unitSlot in _deployedUnitsPanel.transform) {
            foreach(Transform child in unitSlot) {
                Destroy(child.gameObject);
            }
        }

        if(allyList == null || allyList.Count == 0) {
            return;
        }
        
        // Sort by cost then by name
        allyList.Sort(
            delegate(GameObject g1, GameObject g2) { 
                float g1UnitCost = g1.GetComponent<UnitTraining>().GetUnitCost();
                float g2UnitCost = g2.GetComponent<UnitTraining>().GetUnitCost();
                
                if(g1UnitCost == g2UnitCost) {
                    return g1.GetComponent<Unit>().Name.CompareTo(g2.GetComponent<Unit>().Name);
                } else {
                    return g2UnitCost.CompareTo(g1UnitCost);
                }
                
            });

        if(allyList.Count <= 10) {
            SelectIndividualUnits(allyList);
        } else {
            SelectGroupUnits(allyList);
        }
    }

    private void SelectIndividualUnits(List<GameObject> allyList) {
        int slot = 0;
        List<List<GameObject>> splitAllyList = SplitAllyList(allyList);
        foreach(List<GameObject> sameAllyList in splitAllyList) {
            foreach(GameObject selectedAlly in sameAllyList) {
                GameObject deployedButtonPrefab = _deployedUnitDictionary
                        .GetUnitDeployedButton(selectedAlly
                        .GetComponent<Unit>());
            
                GameObject deployedButton = Instantiate(deployedButtonPrefab, Vector3.zero, Quaternion.identity);
                deployedButton.transform.SetParent(_deployedUnitsPanel.transform.GetChild(slot));
                RectTransform slotRect = deployedButton.GetComponent<RectTransform>();
                slotRect.offsetMin = new Vector2(0, 0);
                slotRect.offsetMax = new Vector2(0, 0);
                slotRect.localScale = new Vector3(1, 1, 1);
                slot++;   

                deployedButton.GetComponent<DeployedUnitButton>().SetUnit(selectedAlly);
            }
        }
    }   

    private void SelectGroupUnits(List<GameObject> allyList) {        
        int slot = 0;
        List<List<GameObject>> splitAllyList = SplitAllyList(allyList);
        foreach(List<GameObject> sameAllyList in splitAllyList) {
            GameObject deployedButtonPrefab_M = _deployedUnitDictionary
                    .GetUnitDeployedButton_M(sameAllyList[0]
                    .GetComponent<Unit>());
        
            GameObject deployedButton = Instantiate(deployedButtonPrefab_M, Vector3.zero, Quaternion.identity);
            deployedButton.transform.SetParent(_deployedUnitsPanel.transform.GetChild(slot));
            RectTransform slotRect = deployedButton.GetComponent<RectTransform>();
            slotRect.offsetMin = new Vector2(0, 0);
            slotRect.offsetMax = new Vector2(0, 0);
            slotRect.localScale = new Vector3(1, 1, 1);
            slot++;   
            TextMeshProUGUI totalUnitsText = deployedButton.transform.GetChild(1).GetChild(0).gameObject.GetComponent<TextMeshProUGUI>();
            totalUnitsText.text = sameAllyList.Count.ToString();

            deployedButton.GetComponent<DeployedUnitButtonM>().SetUnitList(sameAllyList);
        }
    }

    // Assumes that the list is already sorted in a way that the same unit types are together.
    private List<List<GameObject>> SplitAllyList(List<GameObject> allyList) {
        List<List<GameObject>> sameUnitsList = new List<List<GameObject>>();
        List<GameObject> currentUnitList = new List<GameObject>();
        GameObject prevAlly = allyList[0];
        foreach(GameObject selectedAlly in allyList) {
            if(selectedAlly.GetComponent<Unit>().Name == prevAlly.GetComponent<Unit>().Name) {
                currentUnitList.Add(selectedAlly);
            } else {
                sameUnitsList.Add(currentUnitList);
                currentUnitList = new List<GameObject>();
                currentUnitList.Add(selectedAlly);
            }
            prevAlly = selectedAlly;
        }

        sameUnitsList.Add(currentUnitList);

        return sameUnitsList;
    }

    /*================ Units Status ================*/
    public void CreateUnitStatus(DeployedUnitButton deployedButton) {
        GameObject unit = deployedButton.GetUnit();
        _uiUnitStatus = GameObject.Find("UnitStatus").GetComponent<UiUnitStatus>();
        _uiUnitStatus.ChangeUnitStatus(unit);
    }

    public void CreateUnitStatus_M(DeployedUnitButtonM deployedButton) {
        // Assumes the list is not empty
        GameObject unit = deployedButton.GetUnitList()[0];
        _uiUnitStatus = GameObject.Find("UnitStatus").GetComponent<UiUnitStatus>();
        _uiUnitStatus.ChangeUnitStatus(unit);
    }

    /*================ Resources ================*/

    public void UpdateResourcesText(float resources) {
        _resourcesText.text = resources.ToString();
    }

    public void DisplayResourceDeduction(float resourcesDeducted) {
        GameObject popup = Instantiate(_resourceLossPopupPrefab, _resourceChange.transform);        
        ResourceLossPopup resourceLossPopup = popup.GetComponent<ResourceLossPopup>();
        resourceLossPopup.Start();
        resourceLossPopup.SetText(resourcesDeducted);
    }

    public void DisplayResourceGain(float resourcesGained) {
        GameObject popup = Instantiate(_resourceGainPopupPrefab, _resourceChange.transform);        
        ResourceGainPopup resourceGainPopup = popup.GetComponent<ResourceGainPopup>();
        resourceGainPopup.Start();
        resourceGainPopup.SetText(resourcesGained);
    }

    /*================ Train Units ================*/
    
    public void GetTrainingUnitsPanelInfo(List<GameObject> unitTypeList) {
        int slot = 0;
        foreach(GameObject unitType in unitTypeList) {
            if(slot > 10) {
                break;
            }
            Transform unitSummonSlot = _trainUnitsPanel.transform.GetChild(slot);
            GameObject unitPanel = Instantiate(unitType, unitType.transform.position, unitType.transform.rotation);
            unitPanel.transform.SetParent(unitSummonSlot);
            RectTransform slotRect = unitPanel.GetComponent<RectTransform>();
            slotRect.offsetMin = new Vector2(0, 0);
            slotRect.offsetMax = new Vector2(0, 0);
            slotRect.localScale = new Vector3(1, 1, 1);
            slot++;
        }
    }

    public void UpdateTrainingQueue(int queueSize, int maxQueueSize) {
        for(int i = 0; i < maxQueueSize; i++) {
            if(i < queueSize) {
                _trainingQueue.transform.GetChild(i).gameObject.SetActive(true);
            } else {
                _trainingQueue.transform.GetChild(i).gameObject.SetActive(false);
            }
        }
        
    }
    
    /* =============== Pop Up ================= */

    public void PopUpUnitDescription(GameObject unit) {
        GameObject popUp = Instantiate(_popUpPrefab);
        popUp.GetComponent<UiPopUp>().Configure(unit, _popUpPanel);
    }

    public void RemoveUnitDescription(GameObject unit) {
        foreach(Transform child in _popUpPanel.transform) {
            Destroy(child.gameObject);
        }
    }

    /* =============== Other UI ================ */
    
    private bool IsPointerNotOverUI() {
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
        eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
        return results.Count == 0;
    }

    private void MoveCameraThroughMinimap(Vector3 mousePos) {
        // This are magic numbers because i have no idea how tf to make them work.
        mousePos.x -= 10;
        mousePos.y -= 10;

        mousePos.x /= 1.5f;
        mousePos.y /= 1.5f;

        Vector3 worldPos = _minimapCamera.ScreenToWorldPoint(mousePos);
        PlayerCameraManager.Instance.SetCameraPosition(worldPos);
    }
}
