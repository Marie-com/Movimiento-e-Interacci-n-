using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class BoardPlacer : MonoBehaviour
{
    [SerializeField] private GameObject boardPrefab;
    [SerializeField] private bool probarSinRA = false; // para probar en el computador
    [SerializeField, Range(0.0f, 0.3f)] private float margenPantalla = 0.03f;

    private ARTrackedImageManager imageManager;
    private GameObject board;

    void Awake()
    {
        imageManager = GetComponent<ARTrackedImageManager>();
    }

    void Start()
    {
        if (probarSinRA)
            board = Instantiate(boardPrefab, new Vector3(0f, -0.15f, 0.4f), Quaternion.identity);
    }

    void OnEnable()
    {
        imageManager.trackablesChanged.AddListener(OnImagesChanged);
    }

    void OnDisable()
    {
        imageManager.trackablesChanged.RemoveListener(OnImagesChanged);
    }

    void OnImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var image in args.added) TryPlace(image);
        foreach (var image in args.updated) TryPlace(image);
    }

    // El tablero vive en el mundo, NO pegado a la imagen.
    // La primera vez se coloca en cuanto se detecta la imagen.
    // Después solo se recalibra cuando la imagen se ve completa en pantalla.
    // Si la imagen sale de cuadro o se pierde, el tablero se queda donde estaba.
    void TryPlace(ARTrackedImage image)
    {
        if (image.trackingState != TrackingState.Tracking) return;

        if (board == null)
        {
            board = Instantiate(boardPrefab);
            board.transform.SetPositionAndRotation(image.transform.position, image.transform.rotation);
            return;
        }

        if (FullyInView(image))
            board.transform.SetPositionAndRotation(image.transform.position, image.transform.rotation);
    }

    bool FullyInView(ARTrackedImage image)
    {
        Camera cam = Camera.main;
        if (cam == null) return true;

        Vector2 size = image.referenceImage.size;               // tamaño que escribiste en la biblioteca
        if (size.x <= 0f || size.y <= 0f) size = image.size;    // si no lo escribiste, usa el estimado
        Vector2 half = size * 0.5f;

        Vector3[] corners =
        {
            new Vector3(-half.x, 0f, -half.y), new Vector3(half.x, 0f, -half.y),
            new Vector3(-half.x, 0f,  half.y), new Vector3(half.x, 0f,  half.y)
        };

        foreach (var c in corners)
        {
            Vector3 vp = cam.WorldToViewportPoint(image.transform.TransformPoint(c));
            if (vp.z <= 0f) return false;
            if (vp.x < margenPantalla || vp.x > 1f - margenPantalla) return false;
            if (vp.y < margenPantalla || vp.y > 1f - margenPantalla) return false;
        }
        return true;
    }
}