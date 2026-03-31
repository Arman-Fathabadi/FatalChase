using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public class PoliceControllerSetup : Editor
{
    [MenuItem("Waypoint/Spawn Police Pursuit Car")]
    public static void SpawnPoliceCar()
    {
        string prefabPath = "Assets/High Matters/Free American Sedans/Prefabs/Police.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (prefab == null)
        {
            Debug.LogError("Could not find Police prefab at: " + prefabPath);
            return;
        }

        GameObject policeCarObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        Undo.RegisterCreatedObjectUndo(policeCarObj, "Spawn Police Pursuit Car");
        Selection.activeGameObject = policeCarObj;

        PoliceController policeController = policeCarObj.AddComponent<PoliceController>();

        GameObject playerCarForPos = GameObject.Find("SuperminiCar");
        if (playerCarForPos != null)
        {
            policeCarObj.transform.position = playerCarForPos.transform.position - playerCarForPos.transform.forward * 20f;
            policeCarObj.transform.position = new Vector3(policeCarObj.transform.position.x, 0.5f, policeCarObj.transform.position.z);
            policeCarObj.transform.LookAt(new Vector3(playerCarForPos.transform.position.x, 0.5f, playerCarForPos.transform.position.z));
        }

        GameObject playerCar = GameObject.Find("SuperminiCar");
        if (playerCar != null)
        {
            policeController.playerTarget = playerCar.transform;
        }
        else
        {
            // Fallback for scene instances with a different object name.
            SuperminiCarController controller = Object.FindAnyObjectByType<SuperminiCarController>();
            if (controller != null)
            {
                policeController.playerTarget = controller.transform;
            }
            else
            {
                Debug.LogWarning("Could not find SuperminiCar in the scene to assign as playerTarget.");
            }
        }

        string engineAudioPath = "Assets/audios/engine.mp3";
        AudioClip engineClip = AssetDatabase.LoadAssetAtPath<AudioClip>(engineAudioPath);
        if (engineClip != null) policeController.engineClip = engineClip;

        string sirenAudioPath = "Assets/audios/siren.mp3";
        AudioClip sirenClip = AssetDatabase.LoadAssetAtPath<AudioClip>(sirenAudioPath);
        if (sirenClip != null) policeController.sirenClip = sirenClip;

        // Keep the setup object stable until the controller finishes wiring itself.
        Rigidbody rb = policeCarObj.GetComponent<Rigidbody>();
        if (rb == null) rb = policeCarObj.AddComponent<Rigidbody>();
        rb.mass = 2500f;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        Collider existingCollider = policeCarObj.GetComponent<Collider>();
        if (existingCollider != null)
        {
            existingCollider.isTrigger = true;
        }
        else
        {
            BoxCollider box = policeCarObj.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 1.5f, 4f);
            box.center = new Vector3(0, 0.75f, 0);
            box.isTrigger = true;
        }

        GameObject sirenLightObj = new GameObject("SirenLights");
        sirenLightObj.transform.SetParent(policeCarObj.transform, false);
        sirenLightObj.transform.localPosition = new Vector3(0, 1.6f, -0.2f);

        GameObject redLightObj = new GameObject("RedStrobe");
        redLightObj.transform.SetParent(sirenLightObj.transform, false);
        redLightObj.transform.localPosition = new Vector3(-0.4f, 0, 0);
        Light redLight = redLightObj.AddComponent<Light>();
        redLight.type = LightType.Point;
        redLight.color = Color.red;
        redLight.intensity = 5f;
        redLight.range = 8f;

        GameObject blueLightObj = new GameObject("BlueStrobe");
        blueLightObj.transform.SetParent(sirenLightObj.transform, false);
        blueLightObj.transform.localPosition = new Vector3(0.4f, 0, 0);
        Light blueLight = blueLightObj.AddComponent<Light>();
        blueLight.type = LightType.Point;
        blueLight.color = Color.blue;
        blueLight.intensity = 5f;
        blueLight.range = 8f;

        policeController.sirenLightsContainer = sirenLightObj;

        GameObject uiCanvas = GameObject.Find("BustedCanvas");
        if (uiCanvas == null)
        {
            uiCanvas = new GameObject("BustedCanvas");
            Canvas canvas = uiCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.AddComponent<CanvasScaler>();
            uiCanvas.AddComponent<GraphicRaycaster>();

            GameObject bustedTextObj = new GameObject("BustedText");
            bustedTextObj.transform.SetParent(uiCanvas.transform, false);
            Text textComp = bustedTextObj.AddComponent<Text>();

            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.text = "BUSTED!";
            textComp.fontSize = 100;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.color = Color.red;
            textComp.fontStyle = FontStyle.Bold;

            RectTransform rect = textComp.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        GameObject bustingCanvas = null;
        Canvas[] allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach(Canvas c in allCanvases)
        {
            if (c.gameObject.scene.isLoaded && c.name == "BustingCanvas")
            {
                bustingCanvas = c.gameObject;
                break;
            }
        }
        
        Text countdownText = null;
        if (bustingCanvas == null)
        {
            bustingCanvas = new GameObject("BustingCanvas");
            Canvas canvas2 = bustingCanvas.AddComponent<Canvas>();
            canvas2.renderMode = RenderMode.ScreenSpaceOverlay;
            bustingCanvas.AddComponent<CanvasScaler>();
            bustingCanvas.AddComponent<GraphicRaycaster>();
            
            GameObject countdownObj = new GameObject("CountdownText");
            countdownObj.transform.SetParent(bustingCanvas.transform, false);
            countdownText = countdownObj.AddComponent<Text>();
            
            countdownText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            countdownText.text = "BUSTING... 10.0s";
            countdownText.fontSize = 60;
            countdownText.alignment = TextAnchor.UpperCenter;
            countdownText.color = new Color(1f, 0.5f, 0f);
            countdownText.fontStyle = FontStyle.Bold;

            RectTransform rect2 = countdownText.GetComponent<RectTransform>();
            rect2.anchorMin = new Vector2(0, 0.8f);
            rect2.anchorMax = new Vector2(1, 1);
            rect2.offsetMin = Vector2.zero;
            rect2.offsetMax = Vector2.zero;
        }
        else
        {
            countdownText = bustingCanvas.GetComponentInChildren<Text>();
        }
        
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        policeController.bustedUI = uiCanvas;
        policeController.isChasing = true;

        uiCanvas.SetActive(false); 

        Debug.Log("Successfully spawned and configured the police pursuit car!");
    }
}
