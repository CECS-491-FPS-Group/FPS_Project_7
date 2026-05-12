using System;
using TMPro;
using Unity.VisualScripting.FullSerializer;
using UnityEditor;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

public class AverageSliderValue : MonoBehaviour
{
    // averaged value of all children's sliders
    double averageSliderValue {get; set;}

    // all children of GameObject (assigned on Start())
    GameObject[] children;

    // text to put the average on
    TextMeshProUGUI tmp;

    GameObject[] getAllChildren()
    // gets all children of current gameObject and assigns to the children class variable
    {
        GameObject[] localChildren = new GameObject[transform.childCount];
        for (int i = 0; i < localChildren.Length; i++)
            localChildren[i] = transform.GetChild(i).gameObject;

        return localChildren;
    }

    void addSliderListeners()
    {
        for (int i = 0; i < children.Length; i++)
        {

            // skip if child isn't a slider
            if (!children[i].TryGetComponent(out Slider childSlider))
                continue;
            
            // add listener to Slider 
            childSlider.onValueChanged.AddListener(delegate {updateAverageSliderValue(); });
        }
    }

    // recalculate average of all sliders
    void updateAverageSliderValue()
    {
        double avg = 0;
        for (int i = 0; i < children.Length; i++)
        {

            // skip if child isn't a slider
            if (!children[i].TryGetComponent(out Slider childSlider))
                continue;
            
            avg += childSlider.value;
        }
        displayAverageSliderValue(avg/children.Length); 
    }

    void displayAverageSliderValue(double avg)
    {
        tmp.text = avg.ToString();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        children = getAllChildren();
        tmp = transform.Find("AverageSliderValue").GetComponent<TextMeshProUGUI>();
        addSliderListeners();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
