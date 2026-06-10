using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class PlatformMiniGameUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private TextMeshProUGUI dialogText;

    [Header("Timing")]
    [SerializeField] private float typeSpeed = 0.035f;
    [SerializeField] private float delayAfterText = 5f;

    private PlatformMiniGameGrid grid;

    public void Init(PlatformMiniGameGrid miniGameGrid)
    {
        grid = miniGameGrid;
        HideWindow();
    }

    public void HideWindow()
    {
        if (windowRoot)
            windowRoot.SetActive(false);
    }

    public IEnumerator ShowDialog(string message)
    {
        if (windowRoot)
            windowRoot.SetActive(true);

        if (!dialogText)
            yield break;

        dialogText.text = message;
        dialogText.maxVisibleCharacters = 0;

        for (int i = 0; i <= message.Length; i++)
        {
            dialogText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(typeSpeed);
        }

        yield return new WaitForSeconds(delayAfterText);
    }

    public IEnumerator CountdownRoutine(
        int countdownFrom,
        PlatformTile playerTileDuringCountdown)
    {
        for (int i = countdownFrom; i >= 0; i--)
        {
            grid.SetAllPathBlue();

            int digit = Mathf.Clamp(i, 0, 9);
            DrawCountdownNumberWithBorder(digit);

            yield return new WaitForSeconds(1f);
        }

        grid.ResetTiles();
    }

    private void DrawCountdownNumberWithBorder(int number)
    {
        if (grid == null)
            return;

        int cx = (grid.MinX + grid.MaxX) / 2;
        int cy = (grid.MinY + grid.MaxY) / 2;

        int[,] pattern = GetDigitPattern(number);

        // obramowanie
        for (int y = -1; y <= 5; y++)
        {
            for (int x = -1; x <= 3; x++)
            {
                PlatformTile borderTile = grid.GetTile(cx + x - 1, cy + (4 - y) - 2);

                if (borderTile)
                    borderTile.SetWarning();
            }
        }

        // œrodek liczby
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                if (pattern[y, x] == 1)
                {
                    PlatformTile tile = grid.GetTile(cx + x - 1, cy + (4 - y) - 2);

                    if (tile)
                        tile.SetFinish();
                }
            }
        }
    }

    private int[,] GetDigitPattern(int n)
    {
        n = Mathf.Clamp(n, 0, 9);

        int[][,] digits =
        {
            new int[,] {{1,1,1},{1,0,1},{1,0,1},{1,0,1},{1,1,1}}, // 0
            new int[,] {{0,1,0},{1,1,0},{0,1,0},{0,1,0},{1,1,1}}, // 1
            new int[,] {{1,1,1},{0,0,1},{1,1,1},{1,0,0},{1,1,1}}, // 2
            new int[,] {{1,1,1},{0,0,1},{1,1,1},{0,0,1},{1,1,1}}, // 3
            new int[,] {{1,0,1},{1,0,1},{1,1,1},{0,0,1},{0,0,1}}, // 4
            new int[,] {{1,1,1},{1,0,0},{1,1,1},{0,0,1},{1,1,1}}, // 5
            new int[,] {{1,1,1},{1,0,0},{1,1,1},{1,0,1},{1,1,1}}, // 6
            new int[,] {{1,1,1},{0,0,1},{0,1,0},{0,1,0},{0,1,0}}, // 7
            new int[,] {{1,1,1},{1,0,1},{1,1,1},{1,0,1},{1,1,1}}, // 8
            new int[,] {{1,1,1},{1,0,1},{1,1,1},{0,0,1},{1,1,1}}, // 9
        };

        return digits[n];
    }

    public IEnumerator PlayPreCountdownTileAnimationLoop(System.Func<bool> keepRunning)
    {
        if (grid == null)
            yield break;

        int cx = (grid.MinX + grid.MaxX) / 2;
        int cy = (grid.MinY + grid.MaxY) / 2;

        int maxRing = Mathf.Max(
            Mathf.Abs(grid.MaxX - cx),
            Mathf.Abs(grid.MaxY - cy)
        );

        float stepDelay = 0.06f;
        float loopPause = 0.08f;

        bool animateToCenter = true;

        while (keepRunning != null && keepRunning())
        {
            grid.ResetTiles();

            if (animateToCenter)
            {
                // od zewnêtrznych krawêdzi do œrodka
                for (int ring = maxRing; ring >= 0; ring--)
                {
                    if (keepRunning == null || !keepRunning())
                        break;

                    PaintRing(cx, cy, ring);
                    yield return new WaitForSeconds(stepDelay);
                }
            }
            else
            {
                // od œrodka do zewnêtrznych krawêdzi
                for (int ring = 0; ring <= maxRing; ring++)
                {
                    if (keepRunning == null || !keepRunning())
                        break;

                    PaintRing(cx, cy, ring);
                    yield return new WaitForSeconds(stepDelay);
                }
            }

            animateToCenter = !animateToCenter;

            yield return new WaitForSeconds(loopPause);
        }

        grid.ResetTiles();
    }

    private void PaintRing(int cx, int cy, int ring)
    {
        foreach (var tile in PlatformMiniGameGrid.Tiles)
        {
            if (!tile) continue;

            int dx = Mathf.Abs(tile.GridX - cx);
            int dy = Mathf.Abs(tile.GridY - cy);

            int dist = Mathf.Max(dx, dy);

            if (dist != ring)
                continue;

            if ((ring % 3) == 0)
                tile.SetPath();
            else if ((ring % 3) == 1)
                tile.SetSafe();
            else
                tile.SetWarning();
        }
    }

    public IEnumerator CountdownRoutineStep(
    int number,
    PlatformTile playerTileDuringCountdown)
    {
        grid.SetAllPathBlue();

        int digit = Mathf.Clamp(number, 0, 9);
        DrawCountdownNumberWithBorder(digit);

        yield return new WaitForSeconds(1f);

        if (number <= 0)
            grid.ResetTiles();
    }
}