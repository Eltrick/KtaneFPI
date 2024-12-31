using KeepCoding;
using UnityEngine;

public class SimpletonMod : ModuleScript
{
	private KMBombModule _Module;

	[SerializeField]
	private KMSelectable _Button;

	// Use this for initialization
	void Start ()
	{
		_Module = GetComponent<KMBombModule>();

		_Button.Assign(onInteract: () => _Module.HandlePass());
	}
}
