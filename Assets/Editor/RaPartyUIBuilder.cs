using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Construye (de forma idempotente) toda la UI de RA Party en SampleScene y conecta
// los [SerializeField] de GameManager, OnlineMenu, minijuegos y controladores de pantalla.
// Cada ejecución borra los hijos del Canvas y los vuelve a crear desde cero.
public static class RaPartyUIBuilder
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string MaterialFolder = "Assets/UI/Materials";
    const string ControllerName = "RaPartyUI";
    const string AudioName = "AudioManager";
    const string MenuBackgroundPath = "Assets/UI/Backgrounds/menu_background.png";

    enum Style { Green, Blue, Amber, Wood }

    // ---------- Recursos ----------
    static Sprite panelWood, panelWoodDark, btnWood, btnWoodPressed, hex, hexPressed;
    static Sprite green, greenPressed, blue, bluePressed, amber, amberPressed;
    static Sprite pill, corner, dot, glow, cardFrame, barFill, duckSprite;
    static Sprite iPause, iClose, iDice, iSoundOn, iSoundOff, iCrown, iUsers;
    static Sprite menuBackground; // opcional: ilustración de fondo del menú principal
    static TMP_FontAsset font;
    static Material textMat, titleMat;
    static int uiLayer;

    static readonly Color Cream = UIThemeConfig.Cream;
    static readonly Color Gold = UIThemeConfig.Gold;

    // =====================================================================
    // Entradas
    // =====================================================================
    [MenuItem("RA Party/2. Construir y Conectar UI en Escena")]
    public static void BuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog("RA Party",
            "Se reconstruirá por completo el Canvas de SampleScene y se volverán a conectar las referencias.\n\n" +
            "AR Session, XR Origin, Main Camera, red y EventSystem no se modifican.", "Construir", "Cancelar"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var problems = Build();
        EditorUtility.DisplayDialog("RA Party", problems.Count == 0
            ? "UI construida y conectada sin referencias vacías."
            : "UI construida con avisos:\n- " + string.Join("\n- ", problems), "OK");
    }

    // Para ejecutar sin abrir el editor:
    // Unity -batchmode -quit -projectPath <ruta> -executeMethod RaPartyUIBuilder.BuildFromCommandLine
    public static void BuildFromCommandLine()
    {
        try
        {
            int sprites = UISpriteGenerator.GenerateAll();
            Debug.Log("[RaPartyUIBuilder] Sprites generados: " + sprites);
            var problems = Build();
            foreach (var p in problems) Debug.LogError("[RaPartyUIBuilder] " + p);
            Debug.Log("[RaPartyUIBuilder] Terminado. Avisos: " + problems.Count);
            EditorApplication.Exit(problems.Count == 0 ? 0 : 2);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    public static List<string> Build()
    {
        var problems = new List<string>();
        if (!UISpriteGenerator.AllSpritesExist()) UISpriteGenerator.GenerateAll();
        LoadResources();

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Componentes existentes que se conectan (no se crean ni se mueven)
        var gm = Find<GameManager>();
        var tap = Find<TapMinigame>();
        var mole = Find<MoleMinigame>();
        var duck = Find<DuckMinigame>();
        var placer = Find<BoardPlacer>();
        if (gm == null || tap == null || mole == null || duck == null)
            throw new Exception("Faltan GameManager o minijuegos en la escena.");

        EnsureEventSystem();
        Canvas canvas = PrepareCanvas();
        DestroyRoot(ControllerName);
        DestroyRoot(AudioName);

        // Raíces de controladores y audio
        var audioGO = new GameObject(AudioName, typeof(AudioSource), typeof(AudioManager));
        var audioSource = audioGO.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        Set(audioGO.GetComponent<AudioManager>(), "source", audioSource);

        var ctrl = new GameObject(ControllerName);
        var onlineMenu = ctrl.AddComponent<OnlineMenu>();
        var hud = ctrl.AddComponent<GameHUD>();
        var scan = ctrl.AddComponent<ARScanOverlay>();
        var duel = ctrl.AddComponent<DuelBetView>();
        var pause = ctrl.AddComponent<PauseMenu>();
        var results = ctrl.AddComponent<ResultsPanel>();

        var safe = Stretch(Node("SafeAreaRoot", canvas.transform));
        safe.gameObject.AddComponent<SafeAreaFitter>();

        // Orden de capas (de fondo a frente)
        BuildScanOverlay(safe, scan, placer);
        BuildHud(safe, gm, hud, pause);
        BuildBetPanel(safe, gm, duel);
        BuildTapPanel(safe, tap);
        BuildMolePanel(safe, mole);
        BuildDuckPanel(safe, duck);
        BuildMainMenu(safe, onlineMenu);
        BuildPausePanel(safe, gm, pause, onlineMenu);
        BuildResultsPanel(safe, results, onlineMenu);

        // Verificación de referencias
        Validate(gm, problems);
        Validate(onlineMenu, problems);
        Validate(tap, problems);
        Validate(mole, problems);
        Validate(duck, problems);
        Validate(hud, problems);
        Validate(scan, problems, "boardPlacer");
        Validate(duel, problems, "leftDot", "rightDot");
        Validate(pause, problems);
        Validate(results, problems);
        Validate(audioGO.GetComponent<AudioManager>(), problems, "clickClip", "diceRollClip", "minigameHitClip", "turnAlertClip", "victoryClip");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return problems;
    }

    // =====================================================================
    // Preparación
    // =====================================================================
    static void LoadResources()
    {
        Sprite L(string n)
        {
            var s = UISpriteGenerator.Load(n);
            if (s == null) throw new Exception("Falta el sprite " + n + ". Ejecuta 'RA Party/1. Generar Sprites UI'.");
            return s;
        }
        panelWood = L("panel_wood"); panelWoodDark = L("panel_wood_dark");
        btnWood = L("btn_wood_normal"); btnWoodPressed = L("btn_wood_pressed");
        hex = L("btn_hex_wood"); hexPressed = L("btn_hex_wood_pressed");
        green = L("btn_green_normal"); greenPressed = L("btn_green_pressed");
        blue = L("btn_blue_normal"); bluePressed = L("btn_blue_pressed");
        amber = L("btn_amber_normal"); amberPressed = L("btn_amber_pressed");
        pill = L("pill_hud"); corner = L("ar_reticle_corner");
        dot = L("dot"); glow = L("glow"); cardFrame = L("card_frame"); barFill = L("bar_fill"); duckSprite = L("duck");
        iPause = L("icon_pause"); iClose = L("icon_close"); iDice = L("icon_dice");
        iSoundOn = L("icon_sound_on"); iSoundOff = L("icon_sound_off"); iCrown = L("icon_crown"); iUsers = L("icon_users");
        L("icon_settings"); L("icon_check"); L("icon_restart");

        menuBackground = LoadMenuBackground();

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new Exception("No se encontró la fuente " + FontPath);

        System.IO.Directory.CreateDirectory(MaterialFolder);
        textMat = EnsureMaterial("LiberationSans SDF - RAParty Outline", m =>
        {
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.25f);
            m.SetColor("_OutlineColor", UIThemeConfig.TextDark);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
            m.SetFloat("_UnderlayOffsetX", 0.35f);
            m.SetFloat("_UnderlayOffsetY", -0.5f);
            m.SetFloat("_UnderlayDilate", 0.3f);
            m.SetFloat("_UnderlaySoftness", 0.35f);
        });
        titleMat = EnsureMaterial("LiberationSans SDF - RAParty Title", m =>
        {
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.28f);
            m.SetColor("_OutlineColor", UIThemeConfig.WoodLip);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", UIThemeConfig.WoodOutline);
            m.SetFloat("_UnderlayOffsetX", 0.55f);
            m.SetFloat("_UnderlayOffsetY", -0.9f);
            m.SetFloat("_UnderlayDilate", 0.45f);
            m.SetFloat("_UnderlaySoftness", 0.05f);
            m.SetFloat("_FaceDilate", 0.1f);
        });
        uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;
    }

    // Importa la ilustración del menú como Sprite (si existe) y la devuelve
    static Sprite LoadMenuBackground()
    {
        var importer = AssetImporter.GetAtPath(MenuBackgroundPath) as TextureImporter;
        if (importer == null) return null;
        if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.maxTextureSize != 2048)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(MenuBackgroundPath);
    }

    static Material EnsureMaterial(string name, Action<Material> setup)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(font.material) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = font.material.shader;
            mat.CopyPropertiesFromMaterial(font.material);
        }
        setup(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static T Find<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    static void DestroyRoot(string name)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name) Object.DestroyImmediate(root);
    }

    static void EnsureEventSystem()
    {
        var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (systems.Length == 0)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return;
        }
        var es = systems[0].gameObject;
        if (es.GetComponent<InputSystemUIInputModule>() == null) es.AddComponent<InputSystemUIInputModule>();
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null) Object.DestroyImmediate(legacy);
        if (systems.Length > 1) Debug.LogWarning("[RaPartyUIBuilder] Hay más de un EventSystem en la escena.");
    }

    static Canvas PrepareCanvas()
    {
        GameObject go = null;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "Canvas" && root.GetComponent<Canvas>() != null) { go = root; break; }
        if (go == null) go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = uiLayer;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 0;

        var scaler = go.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f; // equilibrio entre teléfonos 20:9 y tablets 4:3
        scaler.referencePixelsPerUnit = 100f;
        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();

        for (int i = go.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        return canvas;
    }

    // =====================================================================
    // Capa 0 - ARScanOverlay
    // =====================================================================
    static void BuildScanOverlay(RectTransform safe, ARScanOverlay scan, BoardPlacer placer)
    {
        var root = Stretch(Node("ARScanOverlay", safe));
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var frame = Place(Node("MarcoEscaneo", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1000f, 560f));
        var corners = new Image[4];
        Vector2[] anchors = { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) };
        Vector3[] flips = { new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(1, -1, 1), new Vector3(-1, -1, 1) };
        string[] names = { "EsquinaSupIzq", "EsquinaSupDer", "EsquinaInfIzq", "EsquinaInfDer" };
        for (int i = 0; i < 4; i++)
        {
            var c = Place(Node(names[i], frame), anchors[i], new Vector2(0f, 1f), Vector2.zero, new Vector2(150f, 150f));
            c.localScale = flips[i];
            corners[i] = Img(c, corner, Color.white);
        }

        var hint = Place(Node("PildoraAviso", root), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(840f, 100f));
        Img(hint, pill, Color.white, true, false, 1.2f);
        var icon = Place(Node("Icono", hint), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(52f, 52f));
        Img(icon, corner, Gold);
        var text = Text(hint, "Texto", "Apunta la cámara al tablero físico", 34f, Cream, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 96f, 30f, 8f, 10f);
        AutoSize(text, 20f, 34f);

        Set(scan, "boardPlacer", placer);
        Set(scan, "group", group);
        Set(scan, "frame", frame);
        SetArray(scan, "corners", corners);
        Set(scan, "hintText", text);
    }

    // =====================================================================
    // Capa 1 - HUD de juego
    // =====================================================================
    static void BuildHud(RectTransform safe, GameManager gm, GameHUD hud, PauseMenu pause)
    {
        var root = Stretch(Node("HUD_Juego", safe));
        var group = root.gameObject.AddComponent<CanvasGroup>();

        // ----- Barra superior -----
        var top = Node("BarraSuperior", root);
        top.anchorMin = new Vector2(0f, 1f); top.anchorMax = new Vector2(1f, 1f); top.pivot = new Vector2(0.5f, 1f);
        top.offsetMin = new Vector2(24f, -136f); top.offsetMax = new Vector2(-24f, -20f);
        var hlg = top.gameObject.AddComponent<HorizontalLayoutGroup>();
        ConfigureLayout(hlg, 16f, TextAnchor.MiddleLeft, true);

        var roundPill = Node("PildoraRonda", top);
        Img(roundPill, pill, Color.white, true, false, 1.1f);
        Layout(roundPill, 250f, 92f);
        var roundText = Text(roundPill, "TextoRonda", "RONDA 1 / 8", 34f, Cream, TextAlignmentOptions.Center);
        Stretch(roundText.rectTransform, 18f, 18f, 6f, 10f);
        AutoSize(roundText, 20f, 34f);

        var turnPill = Node("PildoraTurno", top);
        Img(turnPill, pill, Color.white, true, false, 1.1f);
        Layout(turnPill, 370f, 92f);
        var dotBorder = Place(Node("IndicadorBorde", turnPill), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 1f), new Vector2(54f, 54f));
        Img(dotBorder, dot, UIThemeConfig.TextDark);
        var turnDot = Place(Node("IndicadorTurno", dotBorder), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 42f));
        var turnDotImg = Img(turnDot, dot, UIThemeConfig.PlayerColor(0));
        var turnText = Text(turnPill, "TurnText", "Turno: Rojo", 32f, Color.white, TextAlignmentOptions.MidlineLeft);
        Stretch(turnText.rectTransform, 86f, 18f, 6f, 10f);
        AutoSize(turnText, 18f, 32f);
        turnText.textWrappingMode = TextWrappingModes.NoWrap;

        var spacer = Node("Espaciador", top);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var cardsRow = Node("TarjetasJugadores", top);
        var cardsLayout = cardsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        ConfigureLayout(cardsLayout, 10f, TextAnchor.MiddleRight, true);
        var cards = new PlayerCardView[GameManager.MaxPlayers];
        for (int i = 0; i < cards.Length; i++) cards[i] = BuildPlayerCard(cardsRow, i);

        var pauseBtn = HexButton(top, "BtnPausa", iPause, 104f);
        Layout((RectTransform)pauseBtn.transform, 104f, 104f);

        // ----- Banner de mensajes (toast) -----
        var banner = Node("InfoBanner", root);
        banner.anchorMin = new Vector2(0f, 0f); banner.anchorMax = new Vector2(1f, 0f); banner.pivot = new Vector2(0.5f, 0.5f);
        banner.offsetMin = new Vector2(40f, 36f); banner.offsetMax = new Vector2(-510f, 146f);
        Img(banner, panelWoodDark, Color.white, true, false, 1.6f);
        var bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        bannerGroup.blocksRaycasts = false;
        bannerGroup.interactable = false;
        var diceText = Text(banner, "DiceText", "", 34f, Color.white, TextAlignmentOptions.Center);
        Stretch(diceText.rectTransform, 34f, 34f, 12f, 16f);
        AutoSize(diceText, 20f, 36f);

        // ----- Botón de dado -----
        var pulse = Place(Node("TirarPulso", root), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-36f - 225f, 30f + 76f), new Vector2(450f, 152f));
        var rollGlow = Stretch(Node("Brillo", pulse), -50f, -50f, -50f, -50f);
        var rollGlowImg = Img(rollGlow, glow, new Color(UIThemeConfig.GreenTop.r, UIThemeConfig.GreenTop.g, UIThemeConfig.GreenTop.b, 0f));
        rollGlowImg.preserveAspect = false;
        var roll = CtaButton(pulse, "BtnTirar", "TIRAR DADO", Style.Green, new Vector2(450f, 152f), 44f, iDice);
        Stretch((RectTransform)roll.transform);

        Set(gm, "turnText", turnText);
        Set(gm, "diceText", diceText);
        Set(gm, "rollButton", roll);

        Set(hud, "hudGroup", group);
        Set(hud, "roundText", roundText);
        Set(hud, "turnDot", turnDotImg);
        SetArray(hud, "cards", cards);
        Set(hud, "pauseButton", pauseBtn);
        Set(hud, "pauseMenu", pause);
        Set(hud, "rollButton", roll);
        Set(hud, "rollPulse", pulse);
        Set(hud, "rollGlow", rollGlowImg);
        Set(hud, "infoBanner", banner);
        Set(hud, "infoGroup", bannerGroup);
    }

    static PlayerCardView BuildPlayerCard(RectTransform parent, int player)
    {
        Color c = UIThemeConfig.PlayerColor(player);
        var card = Node("Tarjeta_J" + (player + 1), parent);
        Layout(card, 178f, 96f);

        var highlight = Stretch(Node("Resalte", card), -22f, -22f, -22f, -22f);
        var hImg = Img(highlight, glow, new Color(c.r, c.g, c.b, 0.55f));
        hImg.preserveAspect = false;
        highlight.gameObject.SetActive(false);

        var bg = Stretch(Node("Fondo", card));
        Img(bg, panelWoodDark, Color.white, true, false, 2f);
        var frame = Stretch(Node("Marco", card));
        var frameImg = Img(frame, cardFrame, c, true, false, 1.6f);

        var dotRt = Place(Node("Color", card), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -15f), new Vector2(22f, 22f));
        var dotImg = Img(dotRt, dot, c);

        var nameText = Text(card, "Nombre", "J" + (player + 1) + " · " + UIThemeConfig.PlayerName(player), 22f, Cream, TextAlignmentOptions.MidlineLeft);
        var nrt = nameText.rectTransform;
        nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f); nrt.pivot = new Vector2(0.5f, 1f);
        nrt.offsetMin = new Vector2(44f, -40f); nrt.offsetMax = new Vector2(-10f, -8f);
        AutoSize(nameText, 14f, 22f);
        nameText.textWrappingMode = TextWrappingModes.NoWrap;

        var scoreText = Text(card, "Puntos", "0", 42f, Color.white, TextAlignmentOptions.Center);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0f); srt.pivot = new Vector2(0.5f, 0.5f);
        srt.offsetMin = new Vector2(8f, 6f); srt.offsetMax = new Vector2(-8f, 58f);

        var view = card.gameObject.AddComponent<PlayerCardView>();
        Set(view, "frame", frameImg);
        Set(view, "dot", dotImg);
        Set(view, "nameText", nameText);
        Set(view, "scoreText", scoreText);
        Set(view, "turnHighlight", highlight.gameObject);
        return view;
    }

    // =====================================================================
    // Capa 2 - Modal de duelo
    // =====================================================================
    static void BuildBetPanel(RectTransform safe, GameManager gm, DuelBetView duel)
    {
        var root = Stretch(Node("PanelApuesta", safe));
        Backdrop(root, new Color(0.06f, 0.04f, 0.02f, 0.62f));

        var card = Place(Node("Tarjeta", root), Center, Center, Vector2.zero, new Vector2(860f, 650f));
        Img(card, panelWood, Color.white, true);

        var title = TitleText(card, "Titulo", "¡DUELO!", 84f);
        PlaceTop(title.rectTransform, -30f, new Vector2(720f, 104f));

        var vs = Text(card, "Rivales", "Rojo  VS  Azul", 52f, Color.white, TextAlignmentOptions.Center);
        PlaceTop(vs.rectTransform, -140f, new Vector2(760f, 72f));
        AutoSize(vs, 28f, 52f);

        var range = Text(card, "Rango", "Elige tu apuesta", 30f, Cream, TextAlignmentOptions.Center);
        PlaceTop(range.rectTransform, -214f, new Vector2(760f, 44f));

        var betText = Text(card, "TextoApuesta", "Apuesta: 5 pts", 62f, Gold, TextAlignmentOptions.Center);
        PlaceTop(betText.rectTransform, -264f, new Vector2(760f, 84f));

        var slider = BuildSlider(card);
        PlaceTop((RectTransform)slider.transform, -376f, new Vector2(660f, 64f));

        var confirm = CtaButton(card, "BtnApostar", "CONFIRMAR APUESTA", Style.Green, new Vector2(580f, 116f), 40f);
        PlaceBottom((RectTransform)confirm.transform, 44f, new Vector2(580f, 116f));

        root.gameObject.SetActive(false);

        Set(gm, "betPanel", root.gameObject);
        Set(gm, "betSlider", slider);
        Set(gm, "betText", betText);
        Set(gm, "betConfirmButton", confirm);
        Set(duel, "card", card);
        Set(duel, "vsText", vs);
        Set(duel, "rangeText", range);
    }

    static Slider BuildSlider(RectTransform parent)
    {
        var root = Node("Slider", parent);
        var bg = Stretch(Node("Background", root), 0f, 0f, 4f, 4f);
        Img(bg, pill, Color.white, true, true, 1.6f);

        var fillArea = Stretch(Node("Fill Area", root), 18f, 18f, 15f, 15f);
        var fill = Stretch(Node("Fill", fillArea));
        Img(fill, barFill, UIThemeConfig.GreenTop, true, false, 1.3f);

        var handleArea = Stretch(Node("Handle Slide Area", root), 34f, 34f, 0f, 0f);
        var handle = Node("Handle", handleArea);
        handle.anchorMin = new Vector2(0f, 0f); handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(88f, 26f);
        var handleImg = Img(handle, hex, Color.white, false, true);

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 1f;
        slider.maxValue = 10f;
        slider.wholeNumbers = true;
        slider.value = 5f;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        return slider;
    }

    // =====================================================================
    // Capa 3 - Minijuegos
    // =====================================================================
    struct Shell
    {
        public RectTransform root;
        public TextMeshProUGUI title, timer, counts;
    }

    static Shell MinigameShell(RectTransform safe, string name, string title)
    {
        var s = new Shell();
        s.root = Stretch(Node(name, safe));
        Backdrop(s.root, new Color(0.05f, 0.03f, 0.02f, 0.42f));

        var header = Node("Encabezado", s.root);
        header.anchorMin = new Vector2(0f, 1f); header.anchorMax = new Vector2(1f, 1f); header.pivot = new Vector2(0.5f, 1f);
        header.offsetMin = new Vector2(40f, -194f); header.offsetMax = new Vector2(-40f, -24f);
        Img(header, panelWood, Color.white, true);

        var badge = Place(Node("InsigniaTiempo", header), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 2f), new Vector2(150f, 150f));
        Img(badge, hex, Color.white);
        s.timer = Text(badge, "TextoTiempo", "10", 62f, Color.white, TextAlignmentOptions.Center);
        Stretch(s.timer.rectTransform, 8f, 8f, 4f, -4f);
        s.timer.textWrappingMode = TextWrappingModes.NoWrap;

        s.title = Text(header, "TextoTitulo", title, 56f, Cream, TextAlignmentOptions.Center);
        Stretch(s.title.rectTransform, 194f, 360f, 22f, 28f);
        AutoSize(s.title, 28f, 58f);

        var countsCard = Place(Node("Marcador", header), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 2f), new Vector2(320f, 136f));
        Img(countsCard, panelWoodDark, Color.white, true, false, 2f);
        s.counts = Text(countsCard, "TextoContadores", "", 30f, Color.white, TextAlignmentOptions.MidlineLeft);
        Stretch(s.counts.rectTransform, 22f, 16f, 10f, 12f);
        AutoSize(s.counts, 16f, 30f);
        s.counts.lineSpacing = -8f;
        return s;
    }

    static void BuildTapPanel(RectTransform safe, TapMinigame tap)
    {
        var s = MinigameShell(safe, "PanelTap", "¡TOCA LO MÁS RÁPIDO QUE PUEDAS!");
        var btn = CtaButton(s.root, "BtnToque", "¡TOCAR!", Style.Green, new Vector2(660f, 290f), 110f);
        Place((RectTransform)btn.transform, Center, Center, new Vector2(0f, -110f), new Vector2(660f, 290f));
        var juice = btn.GetComponent<UIButtonJuice>();
        SetBool(juice, "playClickSound", false);
        SetFloat(juice, "pressedScale", 0.9f);

        s.root.gameObject.SetActive(false);
        Set(tap, "panel", s.root.gameObject);
        Set(tap, "titleText", s.title);
        Set(tap, "timerText", s.timer);
        Set(tap, "countsText", s.counts);
        Set(tap, "tapButton", btn);
    }

    static void BuildMolePanel(RectTransform safe, MoleMinigame mole)
    {
        var s = MinigameShell(safe, "PanelTopos", "¡GOLPEA LOS TOPOS!");
        var board = Place(Node("TableroTopos", s.root), Center, Center, new Vector2(0f, -100f), new Vector2(620f, 620f));
        Img(board, panelWood, Color.white, true);
        var grid = Stretch(Node("Cuadricula", board), 40f, 40f, 40f, 40f);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(150f, 150f);
        layout.spacing = new Vector2(28f, 28f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;
        layout.childAlignment = TextAnchor.MiddleCenter;

        s.root.gameObject.SetActive(false);
        Set(mole, "panel", s.root.gameObject);
        Set(mole, "grid", grid);
        Set(mole, "titleText", s.title);
        Set(mole, "timerText", s.timer);
        Set(mole, "countsText", s.counts);
        Set(mole, "holeSprite", dot);
    }

    static void BuildDuckPanel(RectTransform safe, DuckMinigame duck)
    {
        var s = MinigameShell(safe, "PanelPatos", "¡DISPARA A LOS PATOS!");
        var field = Stretch(Node("Campo", s.root), 40f, 40f, 210f, 30f);
        field.SetSiblingIndex(1); // detrás del encabezado, delante del fondo
        Img(field, null, new Color(0.47f, 0.78f, 1f, 0.14f), false, true);

        s.root.gameObject.SetActive(false);
        Set(duck, "panel", s.root.gameObject);
        Set(duck, "field", field);
        Set(duck, "titleText", s.title);
        Set(duck, "timerText", s.timer);
        Set(duck, "countsText", s.counts);
        Set(duck, "duckSprite", duckSprite);
        Set(duck, "shotSprite", dot);
    }

    // =====================================================================
    // Capa 4 - Menú principal
    // =====================================================================
    static void BuildMainMenu(RectTransform safe, OnlineMenu menu)
    {
        var root = Stretch(Node("MenuPrincipal", safe));
        if (menuBackground != null)
        {
            // Ilustración a pantalla completa (recorta sin deformar) que tapa la cámara AR en el menú
            var frame = Stretch(Node("FondoIlustracion", root), -150f, -150f, -150f, -150f);
            var art = Node("Imagen", frame);
            Img(art, menuBackground, Color.white, false, true).preserveAspect = false;
            var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = menuBackground.rect.width / menuBackground.rect.height;
            Backdrop(root, new Color(0.06f, 0.04f, 0.02f, 0.12f));
        }
        else
        {
            Backdrop(root, new Color(0.06f, 0.04f, 0.02f, 0.55f));
        }

        var card = Place(Node("Tarjeta", root), Center, Center, new Vector2(0f, -10f), new Vector2(1500f, 860f));
        Img(card, panelWood, Color.white, true);

        var logo = TitleText(card, "Logo", "RA PARTY", 124f);
        logo.characterSpacing = 6f;
        PlaceTop(logo.rectTransform, -24f, new Vector2(1100f, 146f));
        var subtitle = Text(card, "Subtitulo", "Juego de Mesa en Realidad Aumentada", 36f, Cream, TextAlignmentOptions.Center);
        PlaceTop(subtitle.rectTransform, -166f, new Vector2(1100f, 50f));
        var accent = Node("Acento", card);
        PlaceTop(accent, -222f, new Vector2(520f, 10f));
        Img(accent, barFill, Gold, true, false, 4f);

        // ----- Columna online -----
        var online = Place(Node("ColumnaOnline", card), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(56f, 46f), new Vector2(790f, 560f));
        Img(online, panelWoodDark, Color.white, true, false, 1.5f);
        var ovl = online.gameObject.AddComponent<VerticalLayoutGroup>();
        ConfigureVertical(ovl, 12f, new RectOffset(34, 34, 20, 22));

        var oh = Text(online, "Encabezado", "MODO ONLINE", 40f, Gold, TextAlignmentOptions.Center);
        Layout(oh.rectTransform, -1f, 44f);

        var create = CtaButton(online, "BtnCrear", "CREAR SALA", Style.Green, new Vector2(0f, 96f), 42f);
        Layout((RectTransform)create.transform, -1f, 96f);

        var joinRow = Node("FilaUnirse", online);
        Layout(joinRow, -1f, 96f);
        var jl = joinRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        ConfigureLayout(jl, 16f, TextAnchor.MiddleCenter, true);
        jl.childForceExpandHeight = true;
        var input = BuildInput(joinRow, "InputCodigo", "Código de sala...");
        var inputLe = input.gameObject.AddComponent<LayoutElement>();
        inputLe.flexibleWidth = 1f; inputLe.minWidth = 200f;
        var join = CtaButton(joinRow, "BtnUnirse", "UNIRSE", Style.Blue, new Vector2(230f, 96f), 40f);
        Layout((RectTransform)join.transform, 230f, 96f);

        var infoRow = Node("FilaSala", online);
        Layout(infoRow, -1f, 60f);
        var il = infoRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        ConfigureLayout(il, 14f, TextAnchor.MiddleCenter, true);
        il.childForceExpandHeight = true;
        var badge = Node("InsigniaSala", infoRow);
        Img(badge, pill, Color.white, true, false, 1.5f);
        badge.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var codeText = Text(badge, "TextoCodigo", "SALA: ------", 30f, Gold, TextAlignmentOptions.Center);
        Stretch(codeText.rectTransform, 20f, 20f, 4f, 8f);
        AutoSize(codeText, 18f, 30f);
        var playersPill = Node("PildoraJugadores", infoRow);
        Img(playersPill, pill, Color.white, true, false, 1.5f);
        Layout(playersPill, 190f, 60f);
        var usersIcon = Place(Node("Icono", playersPill), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(42f, 42f));
        Img(usersIcon, iUsers, Cream);
        var playersText = Text(playersPill, "TextoJugadores", "-/4", 30f, Color.white, TextAlignmentOptions.Center);
        Stretch(playersText.rectTransform, 64f, 14f, 4f, 8f);

        var status = Text(online, "TextoEstado", "Conectando a los servicios...", 26f, Cream, TextAlignmentOptions.Center);
        Layout(status.rectTransform, -1f, 58f);
        AutoSize(status, 16f, 26f);

        var start = CtaButton(online, "BtnEmpezar", "EMPEZAR PARTIDA", Style.Green, new Vector2(0f, 96f), 40f);
        Layout((RectTransform)start.transform, -1f, 96f);

        // ----- Columna local -----
        var local = Place(Node("ColumnaLocal", card), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-56f, 46f), new Vector2(560f, 560f));
        Img(local, panelWoodDark, Color.white, true, false, 1.5f);
        var lvl = local.gameObject.AddComponent<VerticalLayoutGroup>();
        ConfigureVertical(lvl, 12f, new RectOffset(34, 34, 20, 26));

        var lh = Text(local, "Encabezado", "MODO LOCAL", 40f, Gold, TextAlignmentOptions.Center);
        Layout(lh.rectTransform, -1f, 44f);
        var hint = Text(local, "Indicacion", "¿Cuántos juegan en este dispositivo?", 28f, Cream, TextAlignmentOptions.Center);
        Layout(hint.rectTransform, -1f, 74f);
        AutoSize(hint, 18f, 28f);

        var countRow = Node("FilaJugadores", local);
        Layout(countRow, -1f, 160f);
        var cl = countRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        cl.spacing = 30f; cl.childAlignment = TextAnchor.MiddleCenter;
        cl.childControlWidth = false; cl.childControlHeight = false;
        cl.childForceExpandWidth = false; cl.childForceExpandHeight = false;
        var countButtons = new Button[3];
        var selected = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            var option = Node("Opcion" + (i + 2), countRow);
            option.sizeDelta = new Vector2(140f, 150f);
            var sel = Stretch(Node("Seleccion", option), -26f, -26f, -26f, -26f);
            var selImg = Img(sel, glow, new Color(Gold.r, Gold.g, Gold.b, 0.85f));
            selImg.preserveAspect = false;
            selected[i] = sel.gameObject;
            var b = HexButton(option, "BtnJugadores" + (i + 2), null, 140f);
            Place((RectTransform)b.transform, Center, Center, Vector2.zero, new Vector2(140f, 140f));
            var num = Text(b.transform.Find("Content"), "Numero", (i + 2).ToString(), 66f, Cream, TextAlignmentOptions.Center);
            Stretch(num.rectTransform);
            countButtons[i] = b;
        }

        var filler = Node("Relleno", local);
        filler.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        var playLocal = CtaButton(local, "BtnJugarLocal", "JUGAR EN ESTE DISPOSITIVO", Style.Blue, new Vector2(0f, 110f), 34f);
        Layout((RectTransform)playLocal.transform, -1f, 110f);

        // ----- Botón de audio -----
        var audioBtn = HexButton(root, "BtnAudio", iSoundOn, 110f);
        Place((RectTransform)audioBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(110f, 110f));
        ConfigureAudioToggle(audioBtn, null);

        Set(menu, "menuPanel", root.gameObject);
        Set(menu, "createButton", create);
        Set(menu, "joinButton", join);
        Set(menu, "startButton", start);
        Set(menu, "codeInput", input);
        Set(menu, "statusText", status);
        Set(menu, "roomCodeBadge", badge.gameObject);
        Set(menu, "roomCodeText", codeText);
        Set(menu, "playersText", playersText);
        Set(menu, "localPlayButton", playLocal);
        SetArray(menu, "localCountButtons", countButtons);
        SetArray(menu, "localCountSelected", selected);
    }

    static TMP_InputField BuildInput(RectTransform parent, string name, string placeholder)
    {
        var root = Node(name, parent);
        var bg = Img(root, panelWoodDark, Color.white, true, true, 2f);

        var area = Stretch(Node("Text Area", root), 28f, 28f, 10f, 14f);
        area.gameObject.AddComponent<RectMask2D>();
        var ph = Text(area, "Placeholder", placeholder, 32f, new Color(Cream.r, Cream.g, Cream.b, 0.55f), TextAlignmentOptions.MidlineLeft);
        ph.fontStyle = FontStyles.Italic;
        ph.textWrappingMode = TextWrappingModes.NoWrap;
        var txt = Text(area, "Text", "", 40f, Color.white, TextAlignmentOptions.MidlineLeft);
        txt.textWrappingMode = TextWrappingModes.NoWrap;

        var input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = bg;
        input.textViewport = area;
        input.textComponent = txt;
        input.placeholder = ph;
        input.fontAsset = font;
        input.pointSize = 40f;
        input.characterLimit = 12;
        input.contentType = TMP_InputField.ContentType.Alphanumeric;
        input.customCaretColor = true;
        input.caretColor = Cream;
        input.caretWidth = 3;
        input.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.4f);
        input.onFocusSelectAll = true;
        input.navigation = new Navigation { mode = Navigation.Mode.None };
        input.text = "";
        return input;
    }

    // =====================================================================
    // Capa 5 - Pausa (overlay visual)
    // =====================================================================
    static void BuildPausePanel(RectTransform safe, GameManager gm, PauseMenu pause, OnlineMenu menu)
    {
        var root = Stretch(Node("PanelPausa", safe));
        Backdrop(root, UIThemeConfig.Backdrop);

        var card = Place(Node("Tarjeta", root), Center, Center, Vector2.zero, new Vector2(780f, 820f));
        Img(card, panelWood, Color.white, true);

        var title = TitleText(card, "Titulo", "PAUSA", 96f);
        PlaceTop(title.rectTransform, -28f, new Vector2(600f, 112f));
        var note = Text(card, "NotaOnline", "La partida online sigue activa", 26f, new Color(Cream.r, Cream.g, Cream.b, 0.85f), TextAlignmentOptions.Center);
        PlaceTop(note.rectTransform, -138f, new Vector2(680f, 38f));

        var scores = Node("Puntajes", card);
        PlaceTop(scores, -186f, new Vector2(640f, 204f));
        Img(scores, panelWoodDark, Color.white, true, false, 1.6f);
        var scoresLabel = Text(scores, "Etiqueta", "PUNTAJES", 24f, Gold, TextAlignmentOptions.Center);
        PlaceTop(scoresLabel.rectTransform, -12f, new Vector2(400f, 32f));
        var scoreText = Text(scores, "ScoreText", "", 32f, Color.white, TextAlignmentOptions.Top);
        Stretch(scoreText.rectTransform, 30f, 30f, 48f, 14f);
        AutoSize(scoreText, 16f, 32f);

        var soundRow = Node("FilaSonido", card);
        PlaceTop(soundRow, -406f, new Vector2(640f, 112f));
        var soundBtn = HexButton(soundRow, "BtnSonido", iSoundOn, 104f);
        Place((RectTransform)soundBtn.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(104f, 104f));
        var soundLabel = Text(soundRow, "Etiqueta", "SONIDO: SÍ", 40f, Cream, TextAlignmentOptions.MidlineLeft);
        Stretch(soundLabel.rectTransform, 190f, 20f, 0f, 0f);
        ConfigureAudioToggle(soundBtn, soundLabel);

        var cont = CtaButton(card, "BtnContinuar", "CONTINUAR", Style.Green, new Vector2(560f, 112f), 46f);
        PlaceBottom((RectTransform)cont.transform, 170f, new Vector2(560f, 112f));
        var exit = CtaButton(card, "BtnSalir", "SALIR AL MENÚ", Style.Amber, new Vector2(560f, 112f), 42f);
        PlaceBottom((RectTransform)exit.transform, 42f, new Vector2(560f, 112f));

        var close = HexButton(card, "BtnCerrar", iClose, 96f);
        Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-34f, -34f), new Vector2(96f, 96f));

        root.gameObject.SetActive(false);

        Set(gm, "scoreText", scoreText);
        Set(pause, "panel", root.gameObject);
        Set(pause, "card", card);
        Set(pause, "continueButton", cont);
        Set(pause, "closeButton", close);
        Set(pause, "exitButton", exit);
        Set(pause, "onlineNote", note.gameObject);
        Set(pause, "onlineMenu", menu);
    }

    // =====================================================================
    // Capa 6 - Resultados / podio
    // =====================================================================
    static void BuildResultsPanel(RectTransform safe, ResultsPanel results, OnlineMenu menu)
    {
        var root = Stretch(Node("PanelResultados", safe));
        Backdrop(root, UIThemeConfig.Backdrop);

        var card = Place(Node("Tarjeta", root), Center, Center, new Vector2(0f, -36f), new Vector2(940f, 790f));
        Img(card, panelWood, Color.white, true);

        var crownGlow = Place(Node("BrilloCorona", card), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(300f, 300f));
        Img(crownGlow, glow, new Color(Gold.r, Gold.g, Gold.b, 0.55f));
        var crown = Place(Node("Corona", card), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(160f, 160f));
        var crownImg = Img(crown, iCrown, Gold);

        var title = TitleText(card, "Titulo", "¡FIN DE LA PARTIDA!", 58f);
        PlaceTop(title.rectTransform, -84f, new Vector2(860f, 80f));
        AutoSize(title, 36f, 58f);
        var winner = Text(card, "Ganador", "¡GANA ROJO!", 62f, Color.white, TextAlignmentOptions.Center);
        PlaceTop(winner.rectTransform, -164f, new Vector2(860f, 84f));
        AutoSize(winner, 30f, 62f);

        var list = Node("Ranking", card);
        PlaceTop(list, -256f, new Vector2(780f, 4f * 80f + 3f * 10f));
        var vl = list.gameObject.AddComponent<VerticalLayoutGroup>();
        ConfigureVertical(vl, 10f, new RectOffset(0, 0, 0, 0));

        var rows = new ResultRowView[GameManager.MaxPlayers];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = Node("Fila_" + (i + 1), list);
            Layout(row, -1f, 80f);
            Img(row, panelWoodDark, Color.white, true, false, 2f);
            var frame = Stretch(Node("MarcoGanador", row));
            Img(frame, cardFrame, Gold, true, false, 1.4f);
            frame.gameObject.SetActive(i == 0);

            var rank = Text(row, "Puesto", (i + 1) + "º", 40f, Gold, TextAlignmentOptions.Center);
            Place(rank.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(92f, 70f));
            var rowDot = Place(Node("Color", row), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(122f, 0f), new Vector2(36f, 36f));
            var rowDotImg = Img(rowDot, dot, UIThemeConfig.PlayerColor(i));
            var name = Text(row, "Nombre", UIThemeConfig.PlayerName(i), 40f, Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch(name.rectTransform, 178f, 210f, 4f, 8f);
            var score = Text(row, "Puntaje", "0 pts", 40f, Color.white, TextAlignmentOptions.MidlineRight);
            Place(score.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(200f, 70f));

            var view = row.gameObject.AddComponent<ResultRowView>();
            Set(view, "rankText", rank);
            Set(view, "dot", rowDotImg);
            Set(view, "nameText", name);
            Set(view, "scoreText", score);
            Set(view, "winnerFrame", frame.gameObject);
            rows[i] = view;
        }

        var rematch = CtaButton(card, "BtnRevancha", "REVANCHA", Style.Amber, new Vector2(390f, 112f), 44f);
        Place((RectTransform)rematch.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-210f, 42f), new Vector2(390f, 112f));
        var waiting = Text(card, "EsperandoAnfitrion", "Esperando la revancha del anfitrión...", 28f, Cream, TextAlignmentOptions.Center);
        Place(waiting.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-210f, 42f), new Vector2(390f, 112f));
        AutoSize(waiting, 18f, 28f);
        waiting.gameObject.SetActive(false);
        var menuBtn = CtaButton(card, "BtnMenu", "MENÚ PRINCIPAL", Style.Wood, new Vector2(390f, 112f), 36f);
        Place((RectTransform)menuBtn.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(210f, 42f), new Vector2(390f, 112f));

        root.gameObject.SetActive(false);

        Set(results, "panel", root.gameObject);
        Set(results, "card", card);
        Set(results, "crown", crownImg);
        Set(results, "winnerText", winner);
        SetArray(results, "rows", rows);
        Set(results, "rematchButton", rematch);
        Set(results, "waitingText", waiting);
        Set(results, "menuButton", menuBtn);
        Set(results, "onlineMenu", menu);
    }

    // =====================================================================
    // Botones
    // =====================================================================
    static Button CtaButton(Transform parent, string name, string label, Style style, Vector2 size, float fontSize, Sprite icon = null)
    {
        Sprite normal, pressed;
        float contentTop, contentBottom;
        switch (style)
        {
            case Style.Green: normal = green; pressed = greenPressed; break;
            case Style.Blue: normal = blue; pressed = bluePressed; break;
            case Style.Amber: normal = amber; pressed = amberPressed; break;
            default: normal = btnWood; pressed = btnWoodPressed; break;
        }
        if (style == Style.Wood) { contentTop = 6f; contentBottom = 14f; }
        else { contentTop = 8f; contentBottom = 15f; }

        var rt = Node(name, parent);
        rt.sizeDelta = size;
        var img = Img(rt, normal, Color.white, true, true);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.SpriteSwap;
        btn.spriteState = new SpriteState { pressedSprite = pressed };
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        rt.gameObject.AddComponent<CanvasGroup>();

        var content = Stretch(Node("Content", rt), 26f, 26f, contentTop, contentBottom);
        if (icon != null)
        {
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            var ic = Node("Icon", content);
            Img(ic, icon, Color.white);
            float iconSize = Mathf.Min(76f, size.y * 0.5f);
            Layout(ic, iconSize, iconSize);
            var t = Text(content, "Label", label, fontSize, Color.white, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }
        else
        {
            var t = Text(content, "Label", label, fontSize, Color.white, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(t, 18f, fontSize);
        }

        var juice = rt.gameObject.AddComponent<UIButtonJuice>();
        Set(juice, "content", content);
        Set(juice, "tintTarget", img);
        return btn;
    }

    static Button HexButton(Transform parent, string name, Sprite icon, float size)
    {
        var rt = Node(name, parent);
        rt.sizeDelta = new Vector2(size, size);
        var img = Img(rt, hex, Color.white, false, true);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.SpriteSwap;
        btn.spriteState = new SpriteState { pressedSprite = hexPressed };
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        rt.gameObject.AddComponent<CanvasGroup>();

        float k = size / 128f;
        var content = Stretch(Node("Content", rt));
        content.anchoredPosition = new Vector2(0f, -3f * k); // centro de la cara del hexágono
        if (icon != null)
        {
            var ic = Place(Node("Icon", content), Center, Center, Vector2.zero, new Vector2(size * 0.46f, size * 0.46f));
            Img(ic, icon, Cream);
        }

        var juice = rt.gameObject.AddComponent<UIButtonJuice>();
        Set(juice, "content", content);
        Set(juice, "tintTarget", img);
        SetFloat(juice, "pressOffset", 4f * k);
        return btn;
    }

    static void ConfigureAudioToggle(Button button, TMP_Text label)
    {
        var toggle = button.gameObject.AddComponent<AudioToggleButton>();
        var icon = button.transform.Find("Content/Icon");
        Set(toggle, "icon", icon != null ? icon.GetComponent<Image>() : null);
        Set(toggle, "soundOnIcon", iSoundOn);
        Set(toggle, "soundOffIcon", iSoundOff);
        Set(toggle, "label", label);
        SetBool(button.GetComponent<UIButtonJuice>(), "playClickSound", false);
    }

    // =====================================================================
    // Utilidades de UI
    // =====================================================================
    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = uiLayer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = Center;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        return rt;
    }

    static RectTransform PlaceTop(RectTransform rt, float y, Vector2 size)
    {
        return Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), size);
    }

    static RectTransform PlaceBottom(RectTransform rt, float y, Vector2 size)
    {
        return Place(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), size);
    }

    static Image Img(RectTransform rt, Sprite sprite, Color color, bool sliced = false, bool raycast = false, float pixelsPerUnitMultiplier = 1f)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        if (sliced)
        {
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        }
        else
        {
            img.type = Image.Type.Simple;
            img.preserveAspect = sprite != null;
        }
        return img;
    }

    // Fondo oscuro que cubre también la zona fuera del área segura
    static void Backdrop(RectTransform parent, Color color)
    {
        var bd = Stretch(Node("Fondo", parent), -400f, -400f, -400f, -400f);
        Img(bd, null, color, false, true);
    }

    static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var rt = Stretch(Node(name, parent));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSharedMaterial = textMat;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = alignment;
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    // Título con degradado dorado, contorno grueso y sombra 3D
    static TextMeshProUGUI TitleText(Transform parent, string name, string text, float size)
    {
        var t = Text(parent, name, text, size, Color.white, TextAlignmentOptions.Center);
        t.fontSharedMaterial = titleMat;
        t.enableVertexGradient = true;
        t.colorGradient = new VertexGradient(UIThemeConfig.Hex("FFF1B8"), UIThemeConfig.Hex("FFE08A"), UIThemeConfig.Hex("F5A623"), UIThemeConfig.Hex("E67E22"));
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static void AutoSize(TMP_Text t, float min, float max)
    {
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
    }

    static void Layout(RectTransform rt, float width, float height)
    {
        var le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
        if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
    }

    static void ConfigureLayout(HorizontalLayoutGroup layout, float spacing, TextAnchor alignment, bool controlSize)
    {
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = controlSize;
        layout.childControlHeight = controlSize;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    static void ConfigureVertical(VerticalLayoutGroup layout, float spacing, RectOffset padding)
    {
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    // =====================================================================
    // Conexión de [SerializeField] por SerializedObject
    // =====================================================================
    static SerializedProperty Prop(SerializedObject so, string field)
    {
        var p = so.FindProperty(field);
        if (p == null) throw new Exception("No existe el campo '" + field + "' en " + so.targetObject.GetType().Name);
        return p;
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        Prop(so, field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var p = Prop(so, field);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        Prop(so, field).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        Prop(so, field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Revisa que ningún campo de referencia quede vacío (salvo los opcionales indicados)
    static void Validate(Object target, List<string> problems, params string[] optional)
    {
        var so = new SerializedObject(target);
        var it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.name == "m_Script") continue;
            if (Array.IndexOf(optional, it.name) >= 0) continue;
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null)
                problems.Add(target.GetType().Name + "." + it.name + " está vacío");
            if (it.isArray && it.propertyType == SerializedPropertyType.Generic)
            {
                for (int i = 0; i < it.arraySize; i++)
                {
                    var e = it.GetArrayElementAtIndex(i);
                    if (e.propertyType == SerializedPropertyType.ObjectReference && e.objectReferenceValue == null)
                        problems.Add(target.GetType().Name + "." + it.name + "[" + i + "] está vacío");
                }
            }
        }
    }
}
