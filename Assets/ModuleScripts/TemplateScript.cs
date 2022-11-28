using System;
using System.Collections;
using System.Collections.Generic;
using KModkit;
using KeepCoding;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class TemplateScript : ModuleScript
{
    private KMBombModule _Module;
    private System.Random _Rnd;

    private bool _isModuleSolved, _isSeedSet;
    private int _seed;

    // Use this for initialization
    void Start()
    {
        if (!_isSeedSet)
        {
            _seed = Rnd.Range(int.MinValue, int.MaxValue);
            Log("The seed is: " + _seed.ToString());
            _isSeedSet = true;
        }

        _Rnd = new System.Random(_seed);
        // SET SEED ABOVE IN CASE OF BUGS!!
        // _rnd = new System.Random(loggedSeed);
        _Module = Get<KMBombModule>();
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
