﻿namespace TileMaps
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using Core;

    using DG.Tweening;

    using Sirenix.OdinInspector;

    using UnityEngine;
    using UnityEngine.Assertions;
    using UnityEngine.UI;

    /// <inheritdoc />
    /// <summary>
    ///     This component provide APIs to modify any tiles: swap, destruct, or construct.
    /// </summary>
    public class TileController : MonoBehaviour
    {
        /// <summary>
        ///     Contains a list of selected tiles. Should only have up to 2 elements.
        /// </summary>
        private readonly List<GameObject> selected = new List<GameObject>();

        private readonly List<GameObject> swappingTiles = new List<GameObject>();

        /// <summary>
        ///     Cached reference to the Tile map component.
        /// </summary>
        private Tilemap tilemap;

        private Settings setting;

        private int lastSelected = -1;

        public enum CommandState
        {
            /// <summary>
            ///     Inactive, can not be hovered, nor clicked.
            /// </summary>
            Inactive,

            /// <summary>
            ///     Inactive, can be hovered, can click.
            /// </summary>
            Boooming,

            /// <summary>
            ///     Swapping, can be hovered, also allow selection.
            /// </summary>
            Swapping,

            /// <summary>
            ///     Building, can be hovered, but not selected.
            /// </summary>
            Building,

            /// <summary>
            ///     Deploying characters, different way of hover.
            /// </summary>
            Deploying
        }

        /// <summary>
        ///     Gets a value indicating whether if any tile is currently swapping.
        /// </summary>
        public static CommandState Active { get; private set; }

        public static bool Busy { get; private set; } = true;

        #region For UI button only

        public void BeginInactive()
        {
            this.StopAllCoroutines();

            Active = CommandState.Inactive;
            Time.timeScale = this.setting.timeScale;

            this.ClearSelected();

            // TMP Fast fix
            var panel = GameObject.Find("GroundbreakerPanel");
            for (var i = 0; i < 4; i++)
            {
                var buttonGo = panel.transform.GetChild(i);
                buttonGo.GetComponent<ButtonPressed>().Unpress();
            }
        }

        public void BeginBuild()
        {
            this.Begin(CommandState.Building);
        }

        public void BeginSwap()
        {
            this.Begin(CommandState.Swapping);
        }

        public void BeginBooom()
        {
            this.Begin(CommandState.Boooming);
        }

        public void BeginDeploying(int index)
        {
            var go = FindObjectOfType<PartyManager>();

            Assert.IsTrue(Enumerable.Range(0, 5).Contains(index));
            Assert.IsNotNull(go);

            // Off course skip this
            if (Active == CommandState.Deploying && index == this.lastSelected)
            {
                this.BeginInactive();
                return;
            }

            this.lastSelected = index;

            // this.StartCoroutine(this.PerodicallyIncreaseRisk());

            go.SelectCharacter(index);

            Time.timeScale = 0.00f;

            Active = CommandState.Deploying;

            // TMP Fast fix
            var panel = GameObject.Find("GroundbreakerPanel");
            for (var i = 0; i < 4; i++)
            {
                var buttonGo = panel.transform.GetChild(i);
                buttonGo.GetComponent<ButtonPressed>().Unpress();
            }
        }

        #endregion

        /// <summary>
        /// Start doing command
        /// </summary>
        /// <param name="state">
        /// The state.
        /// </param>
        [Button]
        public void Begin(CommandState state)
        {
            if (Active == state)
            {
                this.BeginInactive();
                return;
            }

            Time.timeScale = 0.00f;

            // Should start inc
            this.StopAllCoroutines();
            this.StartCoroutine(this.PerodicallyIncreaseRisk());

            Active = state;
        }

        /// <summary>
        ///     Clear the selected buffer.
        /// </summary>
        public void ClearSelected()
        {
            foreach (var go in this.selected)
            {
                go.GetComponent<TileStatus>().IsSelected = false;
                go.GetComponent<Hoverable>().Unhover();
            }

            this.selected.Clear();
        }

        /// <summary>
        ///     The select tile.
        /// </summary>
        /// <param name="tile">
        ///     The tile.
        /// </param>
        public void SelectTile(GameObject tile)
        {
            var status = tile.GetComponent<TileStatus>();
            status.IsSelected = true;

            if (this.selected.Contains(tile))
            {
                this.ClearSelected();
                return;
            }

            this.selected.Add(tile);

            if (this.selected.Count < 2)
            {
                return;
            }

            this.SwapSelectedTiles();
        }

        public void BooomTile(GameObject tile)
        {
            var pos = tile.transform.position;
            var blocade = this.tilemap.GetBlockadeAt(pos);

            if (blocade)
            {
                this.tilemap.ChangeTileAt(pos, Tiles.Stone);

                FindObjectOfType<DynamicTerrainController>().IncrementRiskLevel(0.1f);

                GameObject.Find("SFX Manager").GetComponent<SFXManager>().PlaySFX("TileDeploy");
            }
            else
            {
                GameObject.Find("SFX Manager").GetComponent<SFXManager>().PlaySFX("TileError");
            }
        }

        /// <summary>
        ///     Indicate if has something selected.
        /// </summary>
        /// <returns>
        ///     The <see cref="bool"/>.
        /// </returns>
        public bool HasSelected()
        {
            return this.selected.Any();
        }

        /// <summary>
        ///     Must be called after map is setup. To allow interaction.
        /// </summary>
        public void Activate()
        {
            Busy = false;
        }

        protected void OnEnable()
        {
            this.tilemap = this.GetComponent<Tilemap>();
            Active = CommandState.Inactive;

            this.setting = GameObject.FindObjectOfType<Settings>();

            Assert.IsNotNull(this.setting);
        }

        /// <summary>
        ///     Perform an swapping of tiles. Do animation, and swap references.
        /// </summary>
        /// <param name="first">
        ///     The first tile to swap.
        /// </param>
        /// <param name="second">
        ///     The second tile to swap.
        /// </param>
        private void SwapTiles(Vector3 first, Vector3 second)
        {
            Busy = true;

            var tileA = this.tilemap.GetTileBlockAt(first);
            var tileB = this.tilemap.GetTileBlockAt(second);

            this.swappingTiles.Clear();

            // Resetting the reference, temp
            this.tilemap.SetTileBlock(first, tileB.transform);
            this.tilemap.SetTileBlock(second, tileA.transform);

            this.SetRenderOrderFlying(tileA, "HUD", "HUD");
            this.SetRenderOrderFlying(tileB, "HUD", "HUD");

            this.MoveBlockTo(tileA, second);
            this.MoveBlockTo(tileB, first);
        }

        private void MoveBlockTo(GameObject tile, Vector3 destination)
        {
            // Update status
            var tileStatus = tile.GetComponent<TileStatus>();
            tileStatus.IsMoving = true;

            // Calculate travel path
            var origin = tile.transform.position;
            var liftHeight = new Vector3(0.0f, 1.0f, -1.0f);

            var path = new[] { origin + liftHeight, destination + liftHeight, destination };
            var durations = new[] { 0.2f, 0.6f, 0.4f };

            // Perform DOTween sequence
            var sequence = DOTween.Sequence();
            for (var i = 0; i < 2; i++)
            {
                sequence.Append(
                    tile.transform.DOLocalMove(path[i], durations[i])
                                  .SetUpdate(true));
            }

            sequence.Append(
                tile.transform.DOLocalMove(path[2], durations[2])
                              .SetEase(Ease.OutCubic)
                              .SetUpdate(true));

            sequence.OnComplete(() => { this.OnSwapComplete(tile); });

            sequence.SetUpdate(true);
        }

        private IEnumerator PreventBug(GameObject first, GameObject second)
        {
            Time.timeScale = 1.0f;

            yield return new WaitForFixedUpdate();

            first.GetComponent<TileStatus>().IsMoving = false;
            second.GetComponent<TileStatus>().IsMoving = false;

            Time.timeScale = 0.0f;

            // tmp
            FindObjectOfType<DynamicTerrainController>().IncrementRiskLevel(0.15f);
        }

        private void OnSwapComplete(GameObject tile)
        {
            this.swappingTiles.Add(tile);

            if (this.swappingTiles.Count >= 2)
            {
                // Active = false;
                var first = this.swappingTiles[0];
                var second = this.swappingTiles[1];

                this.SetRenderOrderFlying(first, "GroundTiles", "Mobs");
                this.SetRenderOrderFlying(second, "GroundTiles", "Mobs");

                this.StartCoroutine(this.PreventBug(first, second));

                Busy = false;
            }

            this.ClearSelected();
        }

        private void SwapSelectedTiles()
        {
            var first = this.selected[0];
            var second = this.selected[1];

            this.SwapTiles(first.transform.position, second.transform.position);
        }

        private void SetRenderOrderFlying(GameObject tile, string layerName, string childLayer)
        {
            tile.GetComponent<SpriteRenderer>().sortingLayerName = layerName;

            foreach (Transform child in tile.transform)
            {
                var render = child.GetComponent<SpriteRenderer>();
                render.sortingLayerName = childLayer;
            }
        }

        private IEnumerator PerodicallyIncreaseRisk()
        {
            for (;;)
            {
                yield return new WaitForSecondsRealtime(1.0f);

                FindObjectOfType<DynamicTerrainController>().IncrementRiskLevel(0.01f);
            }
        }
    }
}