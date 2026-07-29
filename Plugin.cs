using BepInEx;
using funnymod;
using GorillaNetworking;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Mod1
{
    [BepInPlugin(funnymod.PluginInfo.GUID, funnymod.PluginInfo.Name, funnymod.PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        private AssetBundle tabletBundle;
        private GameObject tabletObject;
        private bool isInitialized;
        private bool wasPrimaryDown;

        private Button joinRandomButton;
        private Button disconnectButton;
        private Button roomInfoButton;
        private GameObject roomInfoTextsGroup;
        private Component roomCodeText;
        private Component masterClientText;
        private Component playersInLobbyText;

        private Transform rightHand;
        private Transform fingerTipBone;
        private GameObject debugSphere;
        private HashSet<Button> pressing = new HashSet<Button>();
        private const float PressDist = 0.04f;

        private void Start()
        {
            HarmonyPatches.ApplyHarmonyPatches();
        }

        private void Update()
        {
            if (!isInitialized && GorillaTagger.Instance?.offlineVRRig != null)
            {
                isInitialized = true;
                rightHand = GorillaTagger.Instance.offlineVRRig.transform.Find("rig/hand.R");
                SetupTablet();
            }

            HandleToggleInput();

            if (tabletObject == null) return;

            bool visible = tabletObject.activeSelf;
            if (debugSphere != null && debugSphere.activeSelf != visible)
                debugSphere.SetActive(visible);

            if (!visible) return;

            CheckFingerPresses();
            UpdateRoomInfo();
        }

        private void OnDisable()
        {
            HarmonyPatches.RemoveHarmonyPatches();
            if (tabletObject != null) Destroy(tabletObject);
            if (debugSphere != null) Destroy(debugSphere);
            if (tabletBundle != null) tabletBundle.Unload(false);
        }

        private void SetupTablet()
        {
            tabletBundle = LoadBundle("funnymod.Assets.tablet");
            if (tabletBundle == null) return;

            GameObject prefab = tabletBundle.LoadAsset<GameObject>("tablet");
            if (prefab == null)
            {
                Debug.LogError("Could not load 'tablet' prefab from bundle");
                foreach (string n in tabletBundle.GetAllAssetNames())
                    Debug.Log(n);
                return;
            }

            tabletObject = Instantiate(prefab);
            tabletObject.name = "TabletMenu";

            Transform leftHand = GorillaTagger.Instance.leftHandTransform;
            if (leftHand != null)
            {
                tabletObject.transform.SetParent(leftHand, false);
                tabletObject.transform.localPosition = new Vector3(0.15f, 0f, 0f);
                tabletObject.transform.localRotation = Quaternion.FromToRotation(Vector3.down, Vector3.back);
                tabletObject.transform.localScale = Vector3.one * 0.18f;
            }

            FindUIElements();
            SetupButtons();
            CreateDebugSphere();
            tabletObject.SetActive(false);
        }

        private void HandleToggleInput()
        {
            if (ControllerInputPoller.instance == null) return;
            bool isDown = ControllerInputPoller.instance.leftControllerPrimaryButton;
            if (isDown && !wasPrimaryDown && tabletObject != null)
                tabletObject.SetActive(!tabletObject.activeSelf);
            wasPrimaryDown = isDown;
        }

        private void CreateDebugSphere()
        {
            debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            debugSphere.name = "FingerTipDebug";
            debugSphere.transform.localScale = Vector3.one * 0.018f;

            Color purple;
            ColorUtility.TryParseHtmlString("#791ABA", out purple);
            var renderer = debugSphere.GetComponent<Renderer>();
            renderer.material.color = purple;
            renderer.enabled = false;

            DontDestroyOnLoad(debugSphere);
        }

        private Transform FindIndexTip(Transform hand)
        {
            foreach (Transform child in hand)
            {
                string name = child.name.ToLower();
                if (name.Contains("index"))
                {
                    Transform tip = child;
                    while (tip.childCount > 0)
                    {
                        Transform deepest = null;
                        foreach (Transform c in tip)
                        {
                            string cn = c.name.ToLower();
                            if (cn.Contains("index") || cn.Contains("tip") || cn.Contains("3") || cn.Contains("distal"))
                            {
                                deepest = c;
                                break;
                            }
                            if (deepest == null) deepest = c;
                        }
                        if (deepest == null) break;
                        tip = deepest;
                    }
                    return tip;
                }
                Transform found = FindIndexTip(child);
                if (found != null) return found;
            }
            return null;
        }

        private void CheckFingerPresses()
        {
            if (rightHand == null) return;

            Vector3 tip = rightHand.position + rightHand.up * 0.16f + rightHand.right * 0.025f;

            if (debugSphere != null)
                debugSphere.transform.position = tip;

            CheckButton(joinRandomButton, tip);
            CheckButton(disconnectButton, tip);
            CheckButton(roomInfoButton, tip);
        }

        private void CheckButton(Button btn, Vector3 tip)
        {
            if (btn == null) return;
            float dist = Vector3.Distance(tip, btn.transform.position);
            bool touching = dist < PressDist;

            if (touching && !pressing.Contains(btn))
            {
                pressing.Add(btn);
                btn.onClick.Invoke();
            }
            else if (!touching)
            {
                pressing.Remove(btn);
            }
        }

        private void FindUIElements()
        {
            joinRandomButton = GetOrAddButton("JoinRandomButton");
            disconnectButton = GetOrAddButton("DisconnectButton");
            roomInfoButton = GetOrAddButton("RoomInfoButton");

            roomInfoTextsGroup = FindChild("RoomInfoTexts");
            if (roomInfoTextsGroup != null)
                roomInfoTextsGroup.SetActive(false);

            roomCodeText = FindTextComponent("RoomCodeText");
            masterClientText = FindTextComponent("MasterClientText");
            playersInLobbyText = FindTextComponent("PlayersInLobbyText");
        }

        private Button GetOrAddButton(string name)
        {
            Transform t = tabletObject.transform.Find(name);
            if (t == null)
            {
                Debug.LogWarning($"Button '{name}' not found");
                return null;
            }
            Button btn = t.GetComponent<Button>();
            if (btn == null)
                btn = t.gameObject.AddComponent<Button>();
            return btn;
        }

        private GameObject FindChild(string name)
        {
            Transform t = FindChildRecursive(tabletObject.transform, name);
            return t != null ? t.gameObject : null;
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform r = FindChildRecursive(child, name);
                if (r != null) return r;
            }
            return null;
        }

        private Component FindTextComponent(string name)
        {
            Transform t = FindChildRecursive(tabletObject.transform, name);
            if (t == null) return null;
            Component c = t.GetComponent<TextMeshProUGUI>();
            if (c != null) return c;
            c = t.GetComponent<TextMeshPro>();
            if (c != null) return c;
            return t.GetComponent<Text>();
        }

        private void SetupButtons()
        {
            SetupButton(joinRandomButton, OnJoinRandom);
            SetupButton(disconnectButton, OnDisconnect);
            SetupButton(roomInfoButton, OnToggleRoomInfo);
        }

        private void SetupButton(Button btn, System.Action action)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => action());
        }

        private void OnJoinRandom()
        {
            if (NetworkSystem.Instance.InRoom)
            {
                NetworkSystem.Instance.ReturnToSinglePlayer();
                StartCoroutine(JoinRandomDelay());
                return;
            }

            GorillaNetworkJoinTrigger trigger = PhotonNetworkController.Instance.currentJoinTrigger ?? GorillaComputer.instance.GetJoinTriggerForZone("forest");
            PhotonNetworkController.Instance.AttemptToJoinPublicRoom(trigger);
        }

        private IEnumerator JoinRandomDelay()
        {
            yield return new WaitForSeconds(1.5f);
            OnJoinRandom();
        }

        private void OnDisconnect()
        {
            if (PhotonNetwork.IsConnected)
                PhotonNetwork.Disconnect();
        }

        private void OnToggleRoomInfo()
        {
            if (roomInfoTextsGroup != null)
                roomInfoTextsGroup.SetActive(!roomInfoTextsGroup.activeSelf);
        }

        private void UpdateRoomInfo()
        {
            if (roomInfoTextsGroup == null || !roomInfoTextsGroup.activeSelf) return;

            if (PhotonNetwork.CurrentRoom != null)
            {
                string code = PhotonNetwork.CurrentRoom.Name;
                string master = "N/A";
                int count = PhotonNetwork.CurrentRoom.PlayerCount;
                int max = PhotonNetwork.CurrentRoom.MaxPlayers;
                foreach (var kvp in PhotonNetwork.CurrentRoom.Players)
                {
                    if (kvp.Value.IsMasterClient)
                    {
                        master = kvp.Value.NickName;
                        break;
                    }
                }

                SetText(roomCodeText, $"Room: {code}");
                SetText(masterClientText, $"Master: {master}");
                SetText(playersInLobbyText, $"Players: {count}/{max}");
            }
            else
            {
                SetText(roomCodeText, "Room: Not in room");
                SetText(masterClientText, "Master: N/A");
                SetText(playersInLobbyText, "Players: 0/0");
            }
        }

        private void SetText(Component c, string text)
        {
            if (c is TMP_Text tmpText)
                tmpText.text = text;
            else if (c is Text legacyText)
                legacyText.text = text;
        }

        private AssetBundle LoadBundle(string name)
        {
            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (s == null)
            {
                Debug.LogError($"Resource not found: {name}");
                foreach (string r in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                    Debug.Log(r);
                return null;
            }
            AssetBundle b = AssetBundle.LoadFromStream(s);
            s.Close();
            return b;
        }
    }
}
