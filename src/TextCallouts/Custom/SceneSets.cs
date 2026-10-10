using System;
using System.Collections.Generic;

namespace TextCallouts.Custom
{
    /// <summary>
    /// The places a scene can be, and the scenery that suits it - as plain numbers, with no reference to
    /// the game, so the loader check can confirm that every Place and Props a recipe names is here.
    ///
    /// The places are points in the world, X Y Z. They are where a scene is anchored, not where anybody
    /// stands: when the scene is built every person and prop is put on the nearest ground there is, so a
    /// point that is a few metres off - over a kerb, in a bush - still gives a scene on the pavement.
    /// </summary>
    internal static class SceneSets
    {
        public static readonly Dictionary<string, float[][]> Places = new Dictionary<string, float[][]>
        {
            // Bolingbroke, outside the walls: the gate, the visitors' car park. Inside is locked to the player.
            { "prison", new[] { P(1855f, 2610f, 45.7f), P(1880f, 2600f, 45.7f), P(1830f, 2620f, 45.6f) } },

            // Vespucci and Del Perro, on the sand above the water line, and Paleto's.
            { "beach", new[] { P(-1380f, -1480f, 4f), P(-1520f, -1210f, 2f), P(-1290f, -1650f, 4f), P(-1720f, -930f, 8f), P(-270f, 6610f, 4f) } },

            { "pier", new[] { P(-1640f, -1040f, 13f), P(-1600f, -1080f, 13f) } },
            { "marina", new[] { P(-800f, -1360f, 5f), P(-870f, -1400f, 2f), P(-1600f, 5250f, 4f) } },

            // Grapeseed's farms, and the ranch in the hills above Vinewood.
            { "farm", new[] { P(2440f, 4975f, 46f), P(1960f, 4800f, 43f), P(2200f, 5100f, 54f), P(1400f, 1120f, 114f) } },

            // The Fleeca branches, the Blaine County bank in Paleto, and the Pacific Standard.
            { "bank", new[] { P(149f, -1040f, 29.4f), P(314f, -279f, 54.2f), P(-1212f, -330f, 37.8f), P(-2962f, 482f, 15.7f),
                              P(1175f, 2706f, 38.1f), P(-112f, 6464f, 31.6f), P(235f, 216f, 106.3f) } },

            // Corner shops and petrol stations, city and county.
            { "store", new[] { P(25.7f, -1347f, 29.5f), P(-47f, -1757f, 29.4f), P(373f, 325f, 103.5f), P(1163f, -323f, 69.2f),
                               P(-707f, -914f, 19.2f), P(1961f, 3740f, 32.3f), P(1729f, 6415f, 35f), P(-3040f, 585f, 7.9f),
                               P(2557f, 382f, 108.6f), P(547f, 2671f, 42f), P(-1487f, -379f, 40f), P(1135f, -982f, 46f) } },

            { "bar", new[] { P(-1388f, -586f, 30.2f), P(-565f, 276f, 83f), P(1985f, 3053f, 47f), P(129f, -1300f, 29.2f) } },

            { "hospital", new[] { P(298f, -584f, 43.3f), P(-449f, -340f, 34.5f), P(340f, -1396f, 32.5f), P(1839f, 3672f, 34.3f), P(-247f, 6331f, 32.4f) } },

            { "airport", new[] { P(-1037f, -2737f, 20.2f), P(1700f, 3260f, 41f) } },
            { "golf", new[] { P(-1350f, 125f, 56f) } },
            { "construction", new[] { P(40f, -400f, 39.9f), P(-150f, -1000f, 27.3f) } },
            { "docks", new[] { P(1000f, -3100f, 5.9f), P(-120f, -2420f, 6f), P(160f, -3080f, 5.8f) } },
            { "sandy", new[] { P(1850f, 3690f, 34.3f), P(1700f, 3600f, 35.4f), P(1960f, 3830f, 32.2f) } },
            { "paleto", new[] { P(-150f, 6300f, 31.4f), P(-300f, 6200f, 31.5f), P(-50f, 6400f, 31.5f) } },
            { "park", new[] { P(1100f, -640f, 56.8f), P(-1800f, -400f, 44f), P(150f, -1000f, 29.3f) } },
            { "forest", new[] { P(-550f, 5300f, 73.5f), P(-800f, 5500f, 32f), P(-1550f, 4400f, 20f) } },
        };

        private static float[] P(float x, float y, float z) { return new[] { x, y, z }; }

        private static PropRecipe O(string model, float x, float y, float heading = 0f)
        {
            return new PropRecipe { Model = model, X = x, Y = y, Heading = heading };
        }

        private static PropRecipe V(string model, float x, float y, float heading, bool damaged)
        {
            return new PropRecipe { Model = model, X = x, Y = y, Heading = heading, Vehicle = true, Damaged = damaged };
        }

        /// <summary>
        /// The scenery, by set. Offsets are metres from the middle of the scene; anything the game turns
        /// out not to have is skipped and named in the log, so a wrong name costs one object, not a scene.
        /// </summary>
        public static readonly Dictionary<string, PropRecipe[]> Sets = new Dictionary<string, PropRecipe[]>
        {
            { "visits", new[] { O("prop_table_03", 0f, 0f), O("prop_chair_01a", -1f, 0.8f, 180f), O("prop_chair_01a", 1f, -0.8f),
                                O("prop_table_03", 3.5f, 1f), O("prop_chair_01a", 3.5f, 2f, 180f), O("prop_mp_barrier_02b", -4f, 3f, 90f) } },
            { "yard", new[] { O("prop_bench_01a", -3f, 2f, 90f), O("prop_bench_01a", 3f, 2f, 270f), O("prop_barbell_01", 0f, 4f),
                              O("prop_mp_barrier_02b", -5f, -2f, 0f), O("prop_mp_barrier_02b", 5f, -2f, 0f) } },
            { "camp", new[] { O("prop_skid_tent_01", -3f, 2f, 20f), O("prop_skid_tent_01b", 2.5f, 3f, 340f), O("prop_skid_tent_03", 0f, 6f, 180f),
                              O("prop_beach_fire", 0f, 0f), O("prop_rub_binbag_01", 4f, -1f), O("prop_rub_cardpile_01", -4f, -1f) } },
            { "beach", new[] { O("prop_beach_towel_01", -1.5f, 0f, 30f), O("prop_beach_towel_02", 1.5f, 0.5f, 350f),
                               O("prop_parasol_04b", 0f, 2f), O("prop_cooler_01", 2.5f, -1f), O("prop_beachball_02", -2.5f, 2f) } },
            { "farm", new[] { O("prop_haybale_01", -3f, 2f, 15f), O("prop_haybale_02", 3f, 3f, 80f), O("prop_haybale_01", 0f, 5f),
                              O("prop_fncwood_14a", -5f, 0f, 90f) } },
            { "crash", new[] { V("asea", -2f, 2f, 30f, true), V("premier", 2.5f, -1f, 200f, true),
                               O("prop_roadcone02a", -2f, -5f), O("prop_roadcone02a", 0f, -6f), O("prop_roadcone02a", 2f, -5f) } },
            { "drugs", new[] { O("prop_drug_package", 0.5f, 0.5f), O("prop_cs_heist_bag_01", -1f, 0.5f, 40f), O("prop_cash_pile_01", 0.8f, -0.4f),
                               O("prop_box_wood02a", 2.5f, 1.5f) } },
            { "party", new[] { O("prop_bbq_1", 0f, 2f), O("prop_chair_08", -2f, 1f, 120f), O("prop_chair_08", 2f, 1f, 240f),
                               O("prop_amb_beer_bottle", 0.6f, 0f), O("prop_amb_beer_bottle", -0.6f, 0.4f), O("prop_boombox_01", 1.5f, 2.5f) } },
            { "dumping", new[] { O("prop_rub_binbag_01", -1f, 1f), O("prop_rub_binbag_03", 0.5f, 1.5f), O("prop_rub_cardpile_01", 1.5f, 0f),
                                 O("prop_rub_tyre_01", -2f, -0.5f), O("prop_rub_couch04", 0f, 3f, 90f) } },
            { "construction", new[] { O("prop_barrier_work05", -3f, -3f), O("prop_barrier_work05", 3f, -3f), O("prop_conc_blocks01a", 0f, 4f),
                                      O("prop_generator_03b", 4f, 2f), O("prop_roadcone02a", -1f, -4f), O("prop_roadcone02a", 1f, -4f) } },
            { "perimeter", new[] { O("prop_mp_barrier_02b", -4f, -6f), O("prop_mp_barrier_02b", 0f, -7f), O("prop_mp_barrier_02b", 4f, -6f),
                                   O("prop_roadcone02a", -6f, -5f), O("prop_roadcone02a", 6f, -5f) } },
            { "cones", new[] { O("prop_roadcone02a", -2f, -4f), O("prop_roadcone02a", 0f, -5f), O("prop_roadcone02a", 2f, -4f) } },
            { "boxes", new[] { O("prop_boxpile_07d", -2f, 2f), O("prop_box_wood02a", 2f, 1f), O("prop_cs_cardbox_01", 0f, 3f) } },
            { "market", new[] { O("prop_table_03", -2f, 2f), O("prop_table_03", 2f, 2f), O("prop_cs_cardbox_01", -2f, 2.4f), O("prop_cs_cardbox_01", 2f, 2.4f) } },
            { "picnic", new[] { O("prop_picnictable_01", 0f, 2f), O("prop_cooler_01", 1.5f, 0.5f), O("prop_bbq_1", -2.5f, 2f) } },
            { "workshop", new[] { O("prop_tool_bench02", 0f, 3f), O("prop_toolchest_01", 2f, 3f), O("prop_tool_box_04", -1.5f, 1f) } },
            { "money", new[] { O("prop_money_bag_01", 0.5f, 0.5f), O("prop_cash_pile_01", -0.5f, 0.6f), O("prop_ld_case_01", 1.2f, -0.3f) } },
            { "tools", new[] { O("prop_tool_box_04", 0.5f, 1f), O("prop_ld_fireaxe", -0.8f, 1f) } },
        };
    }
}
