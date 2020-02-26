﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ResourceGainPopup : MonoBehaviour
{
    TextMeshProUGUI _textMeshPro;
    bool _startHasRun = false;
    byte alpha = 255;

    // Start is called before the first frame update
    public void Start()
    {
        if (_startHasRun)
        {
            return;
        }

        _textMeshPro = gameObject.GetComponent<TextMeshProUGUI>();
        _textMeshPro.fontSize = 50;
        Invoke("Destroy", 1);

        _startHasRun = true;
    }

    public void SetText(float resourcesGained)
    {
        _textMeshPro.text = "+" + resourcesGained.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        _textMeshPro.margin = new Vector4(_textMeshPro.margin.x,
                                          _textMeshPro.margin.y - 1,
                                          _textMeshPro.margin.z,
                                          _textMeshPro.margin.w);

        _textMeshPro.color = new Color32(255,
                                         255,
                                         255,
                                         alpha);

        alpha -= 3;
    }

    void Destroy()
    {
        Destroy(gameObject);
    }
}
