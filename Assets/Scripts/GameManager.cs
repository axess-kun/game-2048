using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
   private enum State
   {
      Initializing,
      SpawnBlocks,
      WaitingInput,
      MovingBlocks,
      Win,
      Lose,
   }

   private struct BlockMoveInfo
   {
      public Node TargetNode;
      public Block BlockToUpdate;
      public Block BlockToDestroy; // For merged only
   }

   private const int WinBlockValue = 2048;

   [Header("Settings")]
   [SerializeField] private Vector2 gridSize = new(4, 4);
   [SerializeField, Range(0f, 1f)] private float spawnRateFirstValue = 0.7f;
   [SerializeField] private int spawnFirstValue = 2;
   [SerializeField] private int spawnSecondValue = 1;
   [SerializeField] private float moveDuration = 0.1f;
   [SerializeField] private BlockType[] blockTypes;

   [Header("Prefab & Parent")]
   [SerializeField] private Node nodePrefab;
   [SerializeField] private Transform nodeParent;
   [SerializeField] private Block blockPrefab;
   [SerializeField] private Transform blockParent;
   [SerializeField] private Transform recycleBlockParent;

   [Header("Etc.")]
   [SerializeField] private SpriteRenderer board;
   [SerializeField] private TextMeshProUGUI moveCountText;
   [SerializeField] private TextMeshProUGUI resultText;

   private readonly Stack<Block> _recycleBlocks = new();
   private readonly List<Node> _nodes = new();
   private readonly List<Block> _blocks = new();
   private readonly Dictionary<int, BlockType> _mapValueBlockType = new();

   private State _state;
   private int _moveCount;

   private void Start()
   {
      RefreshMoveCountText();
      resultText.gameObject.SetActive(false);
      recycleBlockParent.gameObject.SetActive(false);
      ChangeState(State.Initializing);
   }

   private void Update()
   {
      if (_state != State.WaitingInput)
      {
         return;
      }

      if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
      {
         ShiftBlocksAsync(Vector2Int.up).Forget();
      }
      else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
      {
         ShiftBlocksAsync(Vector2Int.down).Forget();
      }
      else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
      {
         ShiftBlocksAsync(Vector2Int.left).Forget();
      }
      else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
      {
         ShiftBlocksAsync(Vector2Int.right).Forget();
      }
   }

   private void ChangeState(State state)
   {
      _state = state;

      switch (state)
      {
         case State.Initializing:
            InitializeGame();
            // InitializeDebug();
            break;
         case State.SpawnBlocks:
            var freeNodeCount = SpawnRandomBlocks(_moveCount == 0 ? 2 : 1);

            // Check after move immediately
            var anyWinBlock = _blocks.Any(block => block.CurrentValue >= WinBlockValue);
            if (anyWinBlock)
            {
               ChangeState(State.Win);
               break;
            }

            // If not win, check other condition
            if (freeNodeCount <= 1)
            {
               ChangeState(State.Lose);
            }
            else
            {
               ChangeState(State.WaitingInput);
            }
            break;
         case State.WaitingInput:
         case State.MovingBlocks:
            // Do nothing
            break;
         case State.Win:
            resultText.gameObject.SetActive(true);
            resultText.text = "WIN";
            resultText.color = Color.green;
            break;
         case State.Lose:
            resultText.gameObject.SetActive(true);
            resultText.text = "LOSE";
            resultText.color = Color.red;
            break;
         default:
            throw new NotImplementedException($"State [{state}] not implemented");
      }
   }

   private void InitializeGame()
   {
      Initialize();
      ChangeState(State.SpawnBlocks);
   }

   // For debug only
   private void InitializeDebug()
   {
      Initialize();

      SpawnBlockAtNode(_nodes.First(node => node.Index == new Vector2Int(0, 0)), 2);
      SpawnBlockAtNode(_nodes.First(node => node.Index == new Vector2Int(0, 1)), 2);
      SpawnBlockAtNode(_nodes.First(node => node.Index == new Vector2Int(0, 2)), 4);
      SpawnBlockAtNode(_nodes.First(node => node.Index == new Vector2Int(0, 3)), 2);
      // SpawnBlockAtNode(_nodes.First(node => node.Index == new Vector2Int(0, 0)), 2);
      // SpawnBlockAtNode(_nodes.First(node => node.Index == new Vector2Int(1, 0)), 2);
      // SpawnBlockAtNode(_nodes.First(node => node.Index == new Vector2Int(2, 0)), 4);
      // SpawnBlockAtNode(_nodes.First(node => node.Index == new Vector2Int(3, 0)), 2);

      ChangeState(State.WaitingInput);
   }

   private void Initialize()
   {
      CacheBlockTypeMap();
      CreateBoard();
      CreateNodeGrids();
   }


   private void CacheBlockTypeMap()
   {
      foreach (var blockType in blockTypes)
      {
         _mapValueBlockType[blockType.value] = blockType;
      }
   }

   private void CreateBoard()
   {
      const float boardSizeAdjust = 0.3f;
      board.size = new Vector2(gridSize.x + boardSizeAdjust, gridSize.y + boardSizeAdjust);
   }

   private void CreateNodeGrids()
   {
      // Start from Left-Bottom
      var adjustX = -(gridSize.x / 2) + 0.5f;
      var adjustY = -(gridSize.y / 2) + 0.5f;
      for (var i = 0; i < gridSize.x; ++i)
      {
         for (var j = 0; j < gridSize.y; ++j)
         {
            var x = adjustX + i;
            var y = adjustY + j;
            var node = Instantiate(nodePrefab, new Vector2(x, y), Quaternion.identity, nodeParent);
            node.Initialize(i, j);
            _nodes.Add(node);
         }
      }
   }

   private int SpawnRandomBlocks(int amount)
   {
      var freeNodes = _nodes.Where(node => node.CurrentBlock == null).OrderBy(_ => Random.value).ToArray();
      var freeCount = freeNodes.Length;
      for (var i = 0; i < amount && i < freeCount; ++i)
      {
         var node = freeNodes[i];
         var randomValue = Random.value <= spawnRateFirstValue ? spawnFirstValue : spawnSecondValue;
         SpawnBlockAtNode(node, randomValue);
      }

      return freeCount;
   }

   private void SpawnBlockAtNode(Node node, int value)
   {
      // Block
      Block block;
      if (_recycleBlocks.Count > 0)
      {
         block = _recycleBlocks.Pop();
      }
      else
      {
         block = Instantiate(blockPrefab);
      }

      block.transform.parent = blockParent;
      block.transform.position = node.WorldPosition;

      var blockType = GetBlockTypeByValue(value);
      block.SetData(blockType);
      block.SetIndex(node.Index);
      _blocks.Add(block);

      // Node
      node.SetBlock(block);
   }

   private BlockType GetBlockTypeByValue(int value)
   {
      // If none, error!
      return _mapValueBlockType[value];
   }

   private async UniTaskVoid ShiftBlocksAsync(Vector2Int direction)
   {
      ++_moveCount;
      RefreshMoveCountText();

      var orderedBlocks = _blocks.OrderBy(block => block.CurrentIndex.x).ThenBy(block => block.CurrentIndex.y).ToList();
      if (direction == Vector2Int.up || direction == Vector2Int.right)
      {
         orderedBlocks.Reverse();
      }

      using var _ = ListPool<UniTask>.Get(out var tasks);
      using var __ = ListPool<BlockMoveInfo>.Get(out var moveMergeInfos);
      using var ___ = ListPool<BlockMoveInfo>.Get(out var moveInfos);

      foreach (var block in orderedBlocks)
      {
         var oldIndex = block.CurrentIndex;
         var newIndex = block.CurrentIndex;
         var isMerged = false;
         // CAUTION: CAN'T add animation logic here! It'll cause infinity loop.
         while (true)
         {
            var nextIndex = newIndex + direction;
            if (!TryGetNodeAtIndex(nextIndex, out var moveTargetNode))
            {
               break;
            }

            // Movable
            var destinationBlock = moveTargetNode.CurrentBlock;
            if (destinationBlock != null)
            {
               // Same value
               if (!destinationBlock.IsMerged && destinationBlock.CanMerge(block.CurrentValue))
               {
                  isMerged = true;
                  // NOTE: To escape infinite loop with dealing with animation
                  block.IncreaseInternalValue();
                  // Set flag
                  block.SetIsMerged(true);
                  // Immediately remove from list
                  _blocks.Remove(destinationBlock);

                  var info = new BlockMoveInfo()
                  {
                     TargetNode = moveTargetNode,
                     BlockToUpdate = block,
                     BlockToDestroy = destinationBlock,
                  };
                  moveMergeInfos.Add(info);
                  newIndex = nextIndex;
               }

               break;
            }

            // Blank area
            newIndex = nextIndex;
         }

         // Not moving
         if (newIndex == oldIndex)
         {
            continue;
         }

         // Immediately set data
         var oldNode = GetNodeAtIndex(oldIndex);
         oldNode.SetBlock(null);
         var targetNode = GetNodeAtIndex(newIndex);
         targetNode.SetBlock(block);

         if (!isMerged)
         {
            var info = new BlockMoveInfo()
            {
               BlockToUpdate = block,
               TargetNode = targetNode,
            };
            moveInfos.Add(info);
         }
      }

      ChangeState(State.MovingBlocks);

      // Animations
      // Only move
      foreach (var info in moveInfos)
      {
         var task = UniTask.Create(async () =>
         {
            var block = info.BlockToUpdate;
            var node = info.TargetNode;
            await block.MoveAsync(node.WorldPosition, moveDuration, destroyCancellationToken);
            block.SetIndex(node.Index);

            node.RefreshDebugName();
         });
         tasks.Add(task);
      }

      // Move & merge
      foreach (var info in moveMergeInfos)
      {
         var task = UniTask.Create(async () =>
         {
            var block = info.BlockToUpdate;
            var node = info.TargetNode;
            await block.MoveAsync(node.WorldPosition, moveDuration, destroyCancellationToken);

            // Update the visual and data
            var blockType = GetBlockTypeByValue(block.CurrentValue);
            block.SetData(blockType);
            block.SetIndex(node.Index);

            // Recycle
            RecycleBlock(info.BlockToDestroy);

            node.RefreshDebugName();
         });
         tasks.Add(task);
      }

      // Wait animation
      await UniTask.WhenAll(tasks);

      // Reset flag
      foreach (var block in _blocks)
      {
         block.SetIsMerged(false);
      }

      ChangeState(State.SpawnBlocks);
   }

   private bool TryGetNodeAtIndex(Vector2Int index, out Node node)
   {
      foreach (var n in _nodes)
      {
         if (n.Index == index)
         {
            node = n;
            return true;
         }
      }

      node = null;
      return false;
   }

   private Node GetNodeAtIndex(Vector2Int index)
   {
      TryGetNodeAtIndex(index, out var node);
      return node;
   }

   private void RecycleBlock(Block block)
   {
      block.transform.SetParent(recycleBlockParent);
      block.SetIsMerged(false);
      _recycleBlocks.Push(block);
   }

   private void RefreshMoveCountText()
   {
      moveCountText.text = $"Move: {_moveCount}";
   }
}
