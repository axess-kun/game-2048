using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class Block : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private TextMeshPro valueText;

    public Vector2Int CurrentIndex { get; private set; }
    public int CurrentValue { get; private set; }
    public bool IsMerged { get; private set; }

    public void SetData(BlockType blockType)
    {
        CurrentValue = blockType.value;
        valueText.text = blockType.value.ToString();
        spriteRenderer.color = blockType.color;
        RefreshDebugName();
    }

    // NOTE: To escape infinite loop when dealing with animation, just update only internal value
    public void IncreaseInternalValue()
    {
        CurrentValue *= 2;
    }

    public void SetIndex(Vector2Int index)
    {
        CurrentIndex = index;
        RefreshDebugName();
    }

    public void SetIsMerged(bool isMerged)
    {
        IsMerged = isMerged;
    }

    public bool CanMerge(int value)
    {
        if (CurrentValue >= 2048)
        {
            return false;
        }

        return CurrentValue == value;
    }

    public async UniTask MoveAsync(Vector3 toPosition, float duration, CancellationToken ct)
    {
        var time = 0f;
        var startPosition = transform.position;
        while (time < duration)
        {
            time += Time.deltaTime;
            var position = Vector3.Lerp(startPosition, toPosition, time / duration);
            transform.position = position;

            // Finish update, no need to wait next frame
            if (time >= duration)
            {
                break;
            }

            await UniTask.NextFrame(ct);
        }
    }

    [Conditional("UNITY_EDITOR")]
    private void RefreshDebugName()
    {
        name = $"Block_{CurrentValue}_{CurrentIndex}";
    }
}
