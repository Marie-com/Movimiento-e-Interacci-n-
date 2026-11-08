public interface IMinigameView
{
    void Show(int participantsMask, int seconds);
    void UpdateCounts(int c0, int c1, int c2, int c3);
    void End();
}
