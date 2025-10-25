using System.Diagnostics;
using TMPro;
using UnityEngine;

public class Node : MonoBehaviour
{
    [SerializeField] private TextMeshPro debugIndexText;

    public Block CurrentBlock { get; private set; }
    public Vector3 WorldPosition => transform.position;
    public Vector2Int Index { get; private set; }

    public void Initialize(int x, int y)
    {
        Index = new(x, y);
        debugIndexText.text = Index.ToString();
        RefreshDebugName();
    }

    public void SetBlock(Block block)
    {
        CurrentBlock = block;
        RefreshDebugName();
    }

    [Conditional("UNITY_EDITOR")]
    public void RefreshDebugName()
    {
        if (CurrentBlock == null)
        {
            name = $"Node_{Index}_Block_NULL";
            return;
        }

        name = $"Node_{Index}_Block_{CurrentBlock.CurrentValue}_{CurrentBlock.CurrentIndex}";
    }
}
