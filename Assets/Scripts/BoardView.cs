using System.Collections;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    [SerializeField] IslandBuilder builder;
    [SerializeField] GameObject piecePrefab;
    [SerializeField] Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };

    Transform[] pieces;

    void Awake()
    {
        builder.GetTile(0); // fuerza a construir la isla ANTES de crear las fichas
        pieces = new Transform[GameManager.MaxPlayers];
        for (int i = 0; i < pieces.Length; i++)
        {
            var go = Instantiate(piecePrefab, builder.transform);
            go.GetComponent<Renderer>().material.color = colors[i];
            pieces[i] = go.transform;
        }
    }

    void OnEnable()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        gm.PieceMoved += OnPieceMoved;
        gm.GameStarted += SyncAll;
        SyncAll(); // al reaparecer el tablero, las fichas quedan donde deben
    }

    void OnDisable()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        gm.PieceMoved -= OnPieceMoved;
        gm.GameStarted -= SyncAll;
    }

    void SyncAll()
    {
        var gm = GameManager.Instance;
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].gameObject.SetActive(gm.started && i < gm.activePlayers);
            pieces[i].localPosition = SlotPosition(i, gm.positions[i]);
        }
    }

    Vector3 SlotPosition(int player, int tile)
    {
        var offset = new Vector3((player % 2 - 0.5f) * 0.006f, 0.008f, (player / 2 - 0.5f) * 0.006f);
        return builder.GetTile(tile).localPosition + offset;
    }

    void OnPieceMoved(int player, int from, int to)
    {
        StartCoroutine(Move(player, from, to));
    }

    IEnumerator Move(int player, int from, int to)
    {
        int total = BoardLayout.TileCount;
        int steps = (to - from + total) % total;
        for (int s = 1; s <= steps; s++)
        {
            int tile = (from + s) % total;
            Vector3 a = pieces[player].localPosition;
            Vector3 b = SlotPosition(player, tile);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.01f;
                pieces[player].localPosition = p;
                yield return null;
            }
            pieces[player].localPosition = b;
        }
    }
}