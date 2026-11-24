using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Slotgun.EditorTools
{
    /// <summary>
    /// Generates the whole playable structure from code:
    ///   Slotgun_Core      — persistent rig (player, HUD, director, lighting). Never unloaded.
    ///   Slotgun_Sector_1..5 — the five arenas, each its own scene so it can be streamed in and freed.
    ///   Slotgun_PrepRoom  — the vestibule between combats (shop + slot machine + exit door).
    /// Re-runnable: it regenerates every scene and the sector catalog in place.
    /// </summary>
    public static class SlotgunBootstrap
    {
        const string ScenesFolder = "Assets/Scenes";
        const string DataFolder = "Assets/Data";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        const string CoreSceneName = "Slotgun_Core";
        const string PrepSceneName = "Slotgun_PrepRoom";
        const string SectorScenePrefix = "Slotgun_Sector_";

        const string CoreScenePath = ScenesFolder + "/" + CoreSceneName + ".unity";
        const string PrepScenePath = ScenesFolder + "/" + PrepSceneName + ".unity";

        const int SectorCount = 5;

        static readonly string[] SectorNames =
        {
            "FOSO DE LOS DADOS",
            "TÚNEL DE LOS BILLETES",
            "CARPA DE LOS ESPEJOS",
            "FÁBRICA DE TRAGAPERRAS",
            "ALTAR DEL JACKPOT"
        };

        [MenuItem("Slotgun/Rebuild All Scenes")]
        public static void RebuildAll()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(ScenesFolder);

            var weaponDef = EnsureWeaponDefinitions();
            var catalog = EnsureSectorCatalog();

            BuildCoreScene(weaponDef, catalog);
            for (int i = 0; i < catalog.Count; i++) BuildSectorScene(catalog, i);
            BuildPrepScene();

            SetBuildSettings(catalog);

            EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Slotgun] Core + {catalog.Count} sectors + vestibule rebuilt.");
        }

        // ---- core scene ------------------------------------------------------

        static void BuildCoreScene(WeaponDefinition weaponDef, SectorCatalog catalog)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ApplyArtBible(null);

            BuildLightingRig();
            BuildEventSystem();

            var player = BuildPlayer(weaponDef);

            var systems = new GameObject("GameSystems");
            systems.AddComponent<SceneStreamer>();
            var director = systems.AddComponent<GameDirector>();
            systems.AddComponent<HUD>();

            director.player = player.GetComponent<PlayerController>();
            director.playerHealth = player.GetComponent<PlayerHealth>();
            director.weapon = player.GetComponentInChildren<Weapon>(true);
            director.loadout = player.GetComponent<PlayerLoadout>();
            director.interactor = player.GetComponent<PlayerInteractor>();
            director.sectors = catalog;
            director.totalRounds = catalog != null && catalog.Count > 0 ? catalog.Count : SectorCount;
            director.coreSceneName = CoreSceneName;
            director.prepSceneName = PrepSceneName;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CoreScenePath);
        }

        static void BuildLightingRig()
        {
            var rigGo = new GameObject("Lighting");
            rigGo.AddComponent<LightingRig>();

            var keyGo = new GameObject("KeyLight");
            keyGo.transform.SetParent(rigGo.transform, false);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = Palette.Props;
            key.intensity = 1.35f;
            key.shadows = LightShadows.Soft;
            keyGo.transform.rotation = Quaternion.Euler(52f, -34f, 0f);

            var rimGo = new GameObject("RimLight");
            rimGo.transform.SetParent(rigGo.transform, false);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = Palette.Accent;
            rim.intensity = 0.5f;
            rim.shadows = LightShadows.None;
            rimGo.transform.rotation = Quaternion.Euler(26f, 152f, 0f);
        }

        static GameObject BuildPlayer(WeaponDefinition weaponDef)
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.2f, 0f);

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 52f;
            cc.stepOffset = 0.4f;

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.62f, 0f);

            var camGo = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
            camGo.transform.SetParent(pivot.transform, false);
            camGo.tag = "MainCamera";

            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Shadow;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 520f;
            cam.fieldOfView = 80f;

            var weaponRoot = new GameObject("Weapon");
            weaponRoot.transform.SetParent(camGo.transform, false);
            weaponRoot.transform.localPosition = new Vector3(0.3f, -0.24f, 0.5f);
            weaponRoot.transform.localScale = Vector3.one * 0.66f;

            var weaponView = new GameObject("ViewModel");
            weaponView.transform.SetParent(weaponRoot.transform, false);
            BuildGunModel(weaponView.transform);

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(weaponRoot.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.03f, 0.56f);

            var pc = player.AddComponent<PlayerController>();
            pc.cam = cam;
            pc.eye = camGo.transform;

            var weapon = weaponRoot.AddComponent<Weapon>();
            weapon.player = pc;
            weapon.muzzle = muzzle.transform;
            weapon.viewmodel = weaponView.transform;
            weapon.definition = weaponDef;
            weapon.spareMags = 3;
            weapon.maxSpareMags = 6;

            // slot 1: the fists, always available
            var fistsGo = new GameObject("Fists");
            fistsGo.transform.SetParent(camGo.transform, false);
            fistsGo.transform.localPosition = new Vector3(0f, -0.42f, 0.55f);
            fistsGo.transform.localScale = Vector3.one * 0.42f;
            var fists = fistsGo.AddComponent<Fists>();
            fists.player = pc;
            BuildFistsModel(fistsGo.transform, fists);

            // slot 3: reserved for weapons added later, empty for now
            var slot3 = new GameObject("WeaponSlot3");
            slot3.transform.SetParent(camGo.transform, false);

            player.AddComponent<PlayerHealth>();
            player.AddComponent<PlayerInteractor>();

            // The playable body: the Blink FREE Low Poly Human, head hidden because the camera
            // sits inside it. Built through PlayerBody so a scene rebuild matches the live scene.
            PlayerBody.Install(player);
            var loadout = player.AddComponent<PlayerLoadout>();
            loadout.fists = fists;
            loadout.slot2 = weapon;
            loadout.slot3 = null;

            var input = player.AddComponent<PlayerInput>();
            input.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            input.defaultActionMap = "Player";
            input.notificationBehavior = PlayerNotifications.SendMessages;
            input.defaultControlScheme = "Keyboard&Mouse";

            // Camera rides the body's head bone; the body's own hands grip the pistol.
            FirstPersonRig.Install(player);

            return player;
        }

static void BuildGunModel(Transform parent)
        {
            // Pistol grip - large base at bottom
            Part(parent, PrimitiveType.Cube, new Vector3(0f, -0.15f, 0.1f), new Vector3(0.2f, 0.3f, 0.15f), Vector3.zero, Palette.CharacterPrimary);
            // Pistol slide/frame - sitting on top of grip
            Part(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0.2f), new Vector3(0.18f, 0.2f, 0.2f), Vector3.zero, Palette.CharacterPrimary);
            // Pistol barrel - extending forward from slide
            Part(parent, PrimitiveType.Cylinder, new Vector3(0.05f, 0f, 0.7f), new Vector3(0.05f, 0.05f, 1.4f), new Vector3(90f, 0f, 0f), Palette.CharacterPrimary);
            // Trigger guard detail
            Part(parent, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.55f), new Vector3(0.12f, 0.1f, 0.15f), Vector3.zero, Palette.Props);
            // Muzzle end cap
            Part(parent, PrimitiveType.Sphere, new Vector3(0.08f, 0f, 1.45f), Vector3.one * 0.2f, Vector3.zero, Palette.Accent, true);
        }

        /// <summary>
        /// Slot 1 has no viewmodel of its own any more. The playable body already has hands, so the
        /// boxes this used to build read as a second, floating pair; a punch is now a reach of the
        /// body's own arm (see FirstPersonArms). The Fists component keeps the damage.
        /// </summary>
        static void BuildFistsModel(Transform parent, Fists fists)
        {
            fists.viewmodel = null;
            fists.leftFist = null;
            fists.rightFist = null;
        }

        // ---- sector scenes ---------------------------------------------------

        static void BuildSectorScene(SectorCatalog catalog, int index)
        {
            var def = catalog.sectors[index];
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ApplyArtBible(def);

            var anchorGo = new GameObject("SectorAnchor");
            var anchor = anchorGo.AddComponent<SectorAnchor>();
            anchor.definition = def;

            BuildSectorGeometry(def, anchor);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SectorScenePath(index + 1));
            Debug.Log($"[Slotgun] Sector {index + 1} '{def.displayName}' -> {SectorScenePath(index + 1)}");
        }

        static void BuildSectorGeometry(SectorDefinition def, SectorAnchor anchor)
        {
            var root = new GameObject("Arena").transform;

            float R = def.arenaRadius;
            float H = def.wallHeight;
            float S = 2f * R + 16f;
            float wallOffset = S * 0.5f - 0.7f;
            float wallLength = S - 1.4f;
            const float Gap = 7f;

            // floor, painted ring, jackpot medallion
            Part(root, PrimitiveType.Cube, new Vector3(0f, -0.5f, 0f), new Vector3(S, 1f, S), Vector3.zero, def.floorTint, false, true);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(R * 0.44f, 0.02f, R * 0.44f), Vector3.zero, def.wallTint);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(R * 0.16f, 0.02f, R * 0.16f), Vector3.zero, Palette.Props, true);

            // three solid walls
            Part(root, PrimitiveType.Cube, new Vector3(0f, H * 0.5f, -wallOffset), new Vector3(wallLength, H, 1.4f), Vector3.zero, def.wallTint, false, true);
            Part(root, PrimitiveType.Cube, new Vector3(wallOffset, H * 0.5f, 0f), new Vector3(1.4f, H, wallLength), Vector3.zero, def.wallTint, false, true);
            Part(root, PrimitiveType.Cube, new Vector3(-wallOffset, H * 0.5f, 0f), new Vector3(1.4f, H, wallLength), Vector3.zero, def.wallTint, false, true);

            // far wall, split around the doorway that leaves the arena
            float segment = wallLength * 0.5f - Gap * 0.5f;
            float segCenter = Gap * 0.5f + segment * 0.5f;
            Part(root, PrimitiveType.Cube, new Vector3(segCenter, H * 0.5f, wallOffset), new Vector3(segment, H, 1.4f), Vector3.zero, def.wallTint, false, true);
            Part(root, PrimitiveType.Cube, new Vector3(-segCenter, H * 0.5f, wallOffset), new Vector3(segment, H, 1.4f), Vector3.zero, def.wallTint, false, true);

            // baseboards
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.22f, -wallOffset + 0.8f), new Vector3(wallLength, 0.44f, 0.3f), Vector3.zero, def.accent);
            Part(root, PrimitiveType.Cube, new Vector3(wallOffset - 0.8f, 0.22f, 0f), new Vector3(0.3f, 0.44f, wallLength), Vector3.zero, def.accent);
            Part(root, PrimitiveType.Cube, new Vector3(-wallOffset + 0.8f, 0.22f, 0f), new Vector3(0.3f, 0.44f, wallLength), Vector3.zero, def.accent);

            // corner pillars with a lantern on top
            float corner = S * 0.5f - 3.5f;
            Vector3[] corners =
            {
                new Vector3(-corner, 0f, -corner), new Vector3(corner, 0f, -corner),
                new Vector3(-corner, 0f, corner), new Vector3(corner, 0f, corner)
            };
            for (int i = 0; i < corners.Length; i++)
            {
                Part(root, PrimitiveType.Cylinder, corners[i] + Vector3.up * (H * 0.45f),
                     new Vector3(1.4f, H * 0.45f, 1.4f), Vector3.zero,
                     i % 2 == 0 ? Palette.CharacterPrimary : Palette.CharacterSecondary, false, true);
                Part(root, PrimitiveType.Sphere, corners[i] + Vector3.up * (H * 0.95f),
                     Vector3.one * 1.8f, Vector3.zero, def.accent, true);
            }

            // the exit door: frame + gate that lifts when the boss dies
            var doorGo = new GameObject("ExitDoor");
            doorGo.transform.SetParent(root, false);
            doorGo.transform.localPosition = new Vector3(0f, 0f, wallOffset);

            Part(doorGo.transform, PrimitiveType.Cube, new Vector3(Gap * 0.5f + 0.9f, H * 0.5f, 0f), new Vector3(1.6f, H, 2.2f), Vector3.zero, Palette.Walls, false, true);
            Part(doorGo.transform, PrimitiveType.Cube, new Vector3(-Gap * 0.5f - 0.9f, H * 0.5f, 0f), new Vector3(1.6f, H, 2.2f), Vector3.zero, Palette.Walls, false, true);
            Part(doorGo.transform, PrimitiveType.Cube, new Vector3(0f, H - 0.6f, 0f), new Vector3(Gap + 3.4f, 1.2f, 2.2f), Vector3.zero, def.accent, true);

            var gate = Part(doorGo.transform, PrimitiveType.Cube, new Vector3(0f, (H - 1.4f) * 0.5f, 0f),
                            new Vector3(Gap, H - 1.4f, 0.6f), Vector3.zero, Palette.Props, false, true);
            gate.name = "Gate";

            var portal = doorGo.AddComponent<DoorPortal>();
            portal.way = DoorWay.ArenaExit;
            portal.gate = gate;
            portal.openLift = H + 1.5f;
            portal.triggerRadius = 3.4f;
            anchor.exitDoor = portal;

            // layout flavour
            switch (def.pattern % 5)
            {
                case 0: BuildDiceLayout(root, R); break;
                case 1: BuildPylonLayout(root, R, H); break;
                case 2: BuildMirrorLayout(root, R, H); break;
                case 3: BuildMachineLayout(root, R, H); break;
                default: BuildAltarLayout(root, R, H); break;
            }

            // sector lighting
            const int lightCount = 6;
            for (int i = 0; i < lightCount; i++)
            {
                float ang = i * (360f / lightCount) + 25f;
                var go = new GameObject("SectorLight_" + i);
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(
                    Mathf.Sin(ang * Mathf.Deg2Rad) * R * 0.78f,
                    H * 1.15f,
                    Mathf.Cos(ang * Mathf.Deg2Rad) * R * 0.78f);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = i % 2 == 0 ? def.accent : Palette.Props;
                light.intensity = 4.5f;
                light.range = R * 1.1f;
                light.shadows = LightShadows.None;
            }

            // scenery from the imported environment packs: the arena footprint never changes,
            // only where the trees and ground cover land and which pack they come from
            SectorScenery.Dress(root, def.arenaRadius, def.pattern);

            // anchor: where the player comes in, where the boss appears, how far it may wander
            var playerSpawn = new GameObject("PlayerSpawn").transform;
            playerSpawn.SetParent(anchor.transform, false);
            playerSpawn.localPosition = new Vector3(0f, 0.2f, -R * 0.52f);

            var bossSpawn = new GameObject("BossSpawn").transform;
            bossSpawn.SetParent(anchor.transform, false);
            bossSpawn.localPosition = new Vector3(0f, 0f, R * 0.45f);

            anchor.playerSpawn = playerSpawn;
            anchor.bossSpawn = bossSpawn;
            anchor.arenaCenter = Vector3.zero;
            anchor.arenaRadius = R - 2f;
        }

        static void BuildDiceLayout(Transform root, float R)
        {
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f + 12f;
                float dist = R * (0.42f + 0.18f * (i % 3));
                float size = 2.4f + (i % 3) * 1.1f;
                var pos = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * dist, size * 0.5f, Mathf.Cos(ang * Mathf.Deg2Rad) * dist);
                Part(root, PrimitiveType.Cube, pos, Vector3.one * size,
                     new Vector3(ang * 0.7f, ang, ang * 0.4f),
                     i % 2 == 0 ? Palette.CharacterPrimary : Palette.Props, false, true);
            }
        }

        static void BuildPylonLayout(Transform root, float R, float H)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    float z = -R * 0.55f + i * (R * 0.28f);
                    var pos = new Vector3(side * R * 0.62f, H * 0.32f, z);
                    Part(root, PrimitiveType.Cylinder, pos, new Vector3(1.8f, H * 0.32f, 1.8f),
                         new Vector3(0f, i * 11f, 4f), Palette.CharacterSecondary, false, true);
                    Part(root, PrimitiveType.Cube, pos + Vector3.up * (H * 0.34f),
                         new Vector3(2.6f, 0.5f, 2.6f), new Vector3(0f, i * 11f, 0f), Palette.Props, true);
                }
            }
        }

        static void BuildMirrorLayout(Transform root, float R, float H)
        {
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f + 30f;
                float dist = R * 0.66f;
                var pos = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * dist, H * 0.4f, Mathf.Cos(ang * Mathf.Deg2Rad) * dist);
                var mirror = Part(root, PrimitiveType.Cube, pos, new Vector3(3.4f, H * 0.8f, 0.5f),
                                  new Vector3(0f, ang + 90f, 6f), Palette.Accent, true, true);
                mirror.name = "Mirror_" + i;
                Part(root, PrimitiveType.Cylinder, pos + Vector3.down * (H * 0.4f),
                     new Vector3(1.4f, 0.25f, 1.4f), Vector3.zero, Palette.Walls, false, true);
            }
        }

        static void BuildMachineLayout(Transform root, float R, float H)
        {
            for (int i = 0; i < 10; i++)
            {
                float ang = i * 36f + 18f;
                float dist = R * (0.52f + 0.2f * (i % 2));
                var pos = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * dist, 1.4f, Mathf.Cos(ang * Mathf.Deg2Rad) * dist);
                Part(root, PrimitiveType.Cube, pos, new Vector3(2.2f, 2.8f, 1.6f),
                     new Vector3(0f, ang + 90f, 0f), Palette.Walls, false, true);
                Part(root, PrimitiveType.Cube, pos + Vector3.up * 0.9f, new Vector3(1.5f, 0.8f, 0.1f),
                     new Vector3(0f, ang + 90f, 0f), Palette.Accent, true);
                Part(root, PrimitiveType.Cylinder, pos + Vector3.down * 1.0f, new Vector3(1.0f, 0.2f, 1.0f),
                     new Vector3(0f, ang, 0f), Palette.Props, true);
            }
        }

        static void BuildAltarLayout(Transform root, float R, float H)
        {
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.35f, 0f), new Vector3(R * 0.24f, 0.35f, R * 0.24f),
                 Vector3.zero, Palette.Walls, false, true);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(R * 0.12f, 0.45f, R * 0.12f),
                 Vector3.zero, Palette.Props, true);

            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f;
                var pos = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * R * 0.68f, 0.6f, Mathf.Cos(ang * Mathf.Deg2Rad) * R * 0.68f);
                Part(root, PrimitiveType.Cylinder, pos, new Vector3(1.1f, 0.6f, 1.1f), Vector3.zero, Palette.CharacterPrimary, false, true);
                Part(root, PrimitiveType.Sphere, pos + Vector3.up * (H * 0.55f), Vector3.one * 1.3f, Vector3.zero, Palette.Accent, true);
            }
        }

        // ---- vestibule -------------------------------------------------------

        static void BuildPrepScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ApplyArtBible(null);

            const float W = 34f;
            const float D = 26f;
            const float H = 6.5f;
            const float Gap = 7f;

            var root = new GameObject("PrepRoom").transform;
            var anchor = root.gameObject.AddComponent<PrepRoomAnchor>();

            float halfW = W * 0.5f;
            float halfD = D * 0.5f;

            Part(root, PrimitiveType.Cube, new Vector3(0f, -0.5f, 0f), new Vector3(W, 1f, D), Vector3.zero, Palette.Floor, false, true);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(6.5f, 0.02f, 6.5f), Vector3.zero, Palette.Walls);

            Part(root, PrimitiveType.Cube, new Vector3(halfW, H * 0.5f, 0f), new Vector3(1.2f, H, D), Vector3.zero, Palette.Walls, false, true);
            Part(root, PrimitiveType.Cube, new Vector3(-halfW, H * 0.5f, 0f), new Vector3(1.2f, H, D), Vector3.zero, Palette.Walls, false, true);
            Part(root, PrimitiveType.Cube, new Vector3(0f, H * 0.5f, -halfD), new Vector3(W, H, 1.2f), Vector3.zero, Palette.Walls, false, true);

            float segment = halfW - Gap * 0.5f;
            float segCenter = Gap * 0.5f + segment * 0.5f;
            Part(root, PrimitiveType.Cube, new Vector3(segCenter, H * 0.5f, halfD), new Vector3(segment, H, 1.2f), Vector3.zero, Palette.Walls, false, true);
            Part(root, PrimitiveType.Cube, new Vector3(-segCenter, H * 0.5f, halfD), new Vector3(segment, H, 1.2f), Vector3.zero, Palette.Walls, false, true);

            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.22f, -halfD + 0.8f), new Vector3(W, 0.44f, 0.3f), Vector3.zero, Palette.Props);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.22f, halfD - 0.8f), new Vector3(W, 0.44f, 0.3f), Vector3.zero, Palette.Props);

            // exit gate to the next sector
            var doorGo = new GameObject("PrepExit");
            doorGo.transform.SetParent(root, false);
            doorGo.transform.localPosition = new Vector3(0f, 0f, halfD);

            Part(doorGo.transform, PrimitiveType.Cube, new Vector3(Gap * 0.5f + 0.9f, H * 0.5f, 0f), new Vector3(1.6f, H, 2.2f), Vector3.zero, Palette.CharacterPrimary, false, true);
            Part(doorGo.transform, PrimitiveType.Cube, new Vector3(-Gap * 0.5f - 0.9f, H * 0.5f, 0f), new Vector3(1.6f, H, 2.2f), Vector3.zero, Palette.CharacterPrimary, false, true);
            Part(doorGo.transform, PrimitiveType.Cube, new Vector3(0f, H - 0.6f, 0f), new Vector3(Gap + 3.4f, 1.2f, 2.2f), Vector3.zero, Palette.Accent, true);

            var gate = Part(doorGo.transform, PrimitiveType.Cube, new Vector3(0f, (H - 1.4f) * 0.5f, 0f),
                            new Vector3(Gap, H - 1.4f, 0.6f), Vector3.zero, Palette.Props, false, true);
            gate.name = "Gate";

            var portal = doorGo.AddComponent<DoorPortal>();
            portal.way = DoorWay.PrepExit;
            portal.gate = gate;
            portal.openLift = H + 1.5f;
            portal.triggerRadius = 3.4f;
            anchor.exitDoor = portal;

            // spawn
            var spawn = new GameObject("PlayerSpawn").transform;
            spawn.SetParent(root, false);
            spawn.localPosition = new Vector3(0f, 0.2f, -halfD + 4f);
            anchor.playerSpawn = spawn;

            // slot machine
            var machineGo = new GameObject("SlotMachine");
            machineGo.transform.SetParent(root, false);
            machineGo.transform.localPosition = new Vector3(-10.5f, 0f, 4.5f);
            machineGo.transform.localRotation = Quaternion.Euler(0f, 135f, 0f);
            BuildSlotMachineBody(machineGo.transform);
            var machine = machineGo.AddComponent<BettingMachine>();
            machineGo.AddComponent<SlotMachineStation>();
            AddProbe(machineGo, 3.4f);
            anchor.machine = machine;

            // stalls
            BuildStall(root, new Vector3(10.5f, 0f, 4.5f), -135f, ShopStation.Kind.Health);
            BuildStall(root, new Vector3(10.5f, 0f, -4.5f), -90f, ShopStation.Kind.Ammo);

            // decor
            for (int i = 0; i < 5; i++)
            {
                float ang = i * 72f + 20f;
                var pos = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 9f, 0.35f, Mathf.Cos(ang * Mathf.Deg2Rad) * 7f);
                Part(root, PrimitiveType.Cylinder, pos, new Vector3(1.5f, 0.35f, 1.5f),
                     new Vector3(0f, i * 26f, 0f), i % 2 == 0 ? Palette.Props : Palette.CharacterSecondary, true);
            }

            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f + 45f;
                var go = new GameObject("PrepLight_" + i);
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * 10f, H * 0.9f, Mathf.Cos(ang * Mathf.Deg2Rad) * 7f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = i % 2 == 0 ? Palette.Props : Palette.Accent;
                light.intensity = 4f;
                light.range = 22f;
                light.shadows = LightShadows.None;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PrepScenePath);
            Debug.Log($"[Slotgun] Vestibule -> {PrepScenePath}");
        }

        static void BuildSlotMachineBody(Transform parent)
        {
            Part(parent, PrimitiveType.Cube, new Vector3(0f, 1.1f, 0f), new Vector3(1.7f, 2.2f, 1f), Vector3.zero, Palette.Walls, false, true);
            Part(parent, PrimitiveType.Cube, new Vector3(0f, 1.75f, 0.5f), new Vector3(1.3f, 0.9f, 0.06f), Vector3.zero, Palette.UIInk);
            for (int i = 0; i < 3; i++)
            {
                Part(parent, PrimitiveType.Cylinder, new Vector3((i - 1) * 0.38f, 1.78f, 0.54f),
                     new Vector3(0.29f, 0.07f, 0.29f), new Vector3(90f, 0f, 0f), Palette.Props, true);
            }
            Part(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.95f, 0.54f), new Vector3(0.24f, 0.1f, 0.24f), new Vector3(90f, 0f, 0f), Palette.CharacterPrimary, true);
            Part(parent, PrimitiveType.Cube, new Vector3(0f, 0.35f, 0.54f), new Vector3(0.95f, 0.18f, 0.12f), Vector3.zero, Palette.Accent, true);
            Part(parent, PrimitiveType.Cube, new Vector3(0f, 2.55f, 0f), new Vector3(2.1f, 0.55f, 0.45f), Vector3.zero, Palette.Props, true);
        }

        static void BuildStall(Transform root, Vector3 position, float yaw, ShopStation.Kind kind)
        {
            var go = new GameObject(kind == ShopStation.Kind.Health ? "HealthStall" : "AmmoStall");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Color main = kind == ShopStation.Kind.Health ? Palette.CharacterSecondary : Palette.Accent;

            Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 1.2f, 1.4f), Vector3.zero, Palette.Walls, false, true);
            Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 1.38f, 0f), new Vector3(2.1f, 0.26f, 1.6f), Vector3.zero, main, true);
            Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 1.95f, 0.25f), new Vector3(0.26f, 0.8f, 0.26f), new Vector3(0f, 0f, 12f), main, true);
            Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 1.95f, 0.25f), new Vector3(0.8f, 0.26f, 0.26f), Vector3.zero, main, true);
            Part(go.transform, PrimitiveType.Sphere, new Vector3(0f, 2.6f, 0f), Vector3.one * 0.5f, Vector3.zero, Palette.Props, true);

            go.AddComponent<ShopStation>().kind = kind;
            AddProbe(go, 3.4f);
        }

        /// <summary>Trigger volume so the player's interaction probe can find the station by walking up to it.</summary>
        static void AddProbe(GameObject go, float size)
        {
            var probe = go.AddComponent<BoxCollider>();
            probe.isTrigger = true;
            probe.center = new Vector3(0f, 1.3f, 0f);
            probe.size = new Vector3(size, 2.8f, size);
        }

        // ---- data assets -----------------------------------------------------

        static SectorCatalog EnsureSectorCatalog()
        {
            string catalogPath = DataFolder + "/SectorCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<SectorCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SectorCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            catalog.sectors = new SectorDefinition[SectorCount];

            for (int i = 0; i < SectorCount; i++)
            {
                string path = $"{DataFolder}/Sector_{i + 1:00}.asset";
                var def = AssetDatabase.LoadAssetAtPath<SectorDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<SectorDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                }

                ApplySectorFlavour(def, i);
                EditorUtility.SetDirty(def);
                catalog.sectors[i] = def;
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        static void ApplySectorFlavour(SectorDefinition def, int index)
        {
            Color[] tints =
            {
                Palette.CharacterPrimary,
                Palette.Props,
                Palette.Accent,
                Palette.CharacterSecondary,
                Color.Lerp(Palette.Props, Palette.Danger, 0.45f)
            };

            def.displayName = SectorNames[index % SectorNames.Length];
            def.sceneName = SectorScenePrefix + (index + 1);
            def.arenaRadius = 30f + index * 3f;          // 30 / 33 / 36 / 39 / 42
            def.wallHeight = 7f + index * 1.2f;
            def.pattern = index;
            def.floorTint = Color.Lerp(Palette.Floor, tints[index], 0.28f);
            def.wallTint = Color.Lerp(Palette.Walls, tints[index], 0.24f);
            def.accent = tints[index];
            def.fogDensity = 0.012f - index * 0.0014f;
        }

        static WeaponDefinition EnsureWeaponDefinitions()
        {
            var slotgun = EnsureWeaponDefinition(
                "Weapon_Slotgun", "SLOTGUN",
                new WeaponStats { damage = 11f, fireRate = 6.5f, magazine = 15, recoil = 0.55f, accuracy = 0.86f, range = 70f },
                pelletsPerShot: 1, spreadMaxDegrees: 7f, automatic: true,
                body: Palette.CharacterSecondary, tracer: Palette.Accent);

            EnsureWeaponDefinition(
                "Weapon_PeaShooter", "TIRACHINAS",
                new WeaponStats { damage = 6f, fireRate = 11f, magazine = 24, recoil = 0.22f, accuracy = 0.94f, range = 90f },
                pelletsPerShot: 1, spreadMaxDegrees: 4f, automatic: true,
                body: Palette.Props, tracer: Palette.CharacterPrimary);

            EnsureWeaponDefinition(
                "Weapon_MoneyCannon", "CAÑÓN DE BILLETES",
                new WeaponStats { damage = 9f, fireRate = 2.1f, magazine = 6, recoil = 1.1f, accuracy = 0.7f, range = 45f },
                pelletsPerShot: 6, spreadMaxDegrees: 11f, automatic: false,
                body: Palette.Accent, tracer: Palette.Props);

            return slotgun;
        }

        /// <summary>Generated assets: always rewritten from code so the data can never drift from the design.</summary>
        static WeaponDefinition EnsureWeaponDefinition(string assetName, string display, WeaponStats stats,
                                                       int pelletsPerShot, float spreadMaxDegrees, bool automatic,
                                                       Color body, Color tracer)
        {
            string path = $"{DataFolder}/{assetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.displayName = display;
            def.baseStats = stats;
            def.pelletsPerShot = pelletsPerShot;
            def.spreadMaxDegrees = spreadMaxDegrees;
            def.automatic = automatic;
            def.bodyColor = body;
            def.tracerColor = tracer;

            EditorUtility.SetDirty(def);
            return def;
        }

        // ---- plumbing --------------------------------------------------------

        static string SectorScenePath(int oneBasedIndex) => $"{ScenesFolder}/{SectorScenePrefix}{oneBasedIndex}.unity";

        static void SetBuildSettings(SectorCatalog catalog)
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(CoreScenePath, true)
            };

            for (int i = 0; i < SectorCount; i++)
                list.Add(new EditorBuildSettingsScene(SectorScenePath(i + 1), true));

            list.Add(new EditorBuildSettingsScene(PrepScenePath, true));

            EditorBuildSettings.scenes = list.ToArray();
        }

        static void ApplyArtBible(SectorDefinition def)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(Palette.Shadow, Palette.Walls, 0.8f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Palette.Shadow;
            RenderSettings.fogDensity = def != null ? def.fogDensity : 0.011f;
        }

        static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler,
                              Color color, bool emissive = false, bool collide = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = $"{type}_{parent.childCount}";

            var collider = go.GetComponent<Collider>();
            if (collider != null && !collide) Object.DestroyImmediate(collider);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            StylizedMaterials.Apply(go, color, emissive);
            return go.transform;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
