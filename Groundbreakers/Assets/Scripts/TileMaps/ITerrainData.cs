﻿namespace TileMaps
{
    using System.Collections.Generic;

    using UnityEngine;

    public interface ITerrainData
    {
        /// <summary>
        ///     The get tile type at this position.
        /// </summary>
        /// <param name="x">
        ///     The x coordinate of the data. Range: [0, 7]
        /// </param>
        /// <param name="y">
        ///     The y coordinate of the data. Range: [0, 7]
        /// </param>
        /// <returns>
        ///     The <see cref="Tiles" />. The enumeration that represent the tile's type.
        /// </returns>
        Tiles GetTileTypeAt(float x, float y);

        /// <summary>
        ///     This function should be called by GameMap.
        ///     For generators: A new sets of data is generated each time you call this Function.
        ///     For custom Terrains: Should Load the right level from our database.
        /// </summary>
        void Initialize();

        /// <summary>
        ///     Get a list of vector3 locations of where the mushrooms are.
        /// </summary>
        /// <returns>
        ///     The <see cref="Vector3"/>.
        /// </returns>
        List<Vector3> GetMushroomLocations();
    }
}