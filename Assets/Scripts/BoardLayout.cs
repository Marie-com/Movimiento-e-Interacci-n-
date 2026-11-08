public enum TileType { Inicio, Puntos, Trampa, Reto }

public static class BoardLayout
{
    public const int TileCount = 30;

    public static TileType TypeAt(int i)
    {
        if (i == 0) return TileType.Inicio;
        if (i == TileCount - 1) return TileType.Puntos; // la cima
        if (i % 5 == 0) return TileType.Reto;
        if (i % 3 == 0) return TileType.Trampa;         // quita puntos
        return TileType.Puntos;                          // da puntos
    }

    // Puntos que gana (+) o pierde (-) quien cae en la casilla
    public static int PointsAt(int i)
    {
        switch (TypeAt(i))
        {
            case TileType.Puntos: return (i % 4 == 0) ? 15 : 10;
            case TileType.Trampa: return -5;
            default: return 0;
        }
    }
}