using System;
using System.Collections.Generic;

namespace CircuitCube.Core
{
    // Level data, format v3. One class per file; JsonUtility reads and writes all of them.
    //   manifest.json            ManifestData   (index of groups: id + unlock rule only)
    //   groups/<id>.json         GroupData      (details, order chunks)
    //   orders/<id>/<k>.json     OrderChunk     (100 positions -> serials)
    //   levels/<id>/<0001>.json  LevelData      (one board, tiles string)
    //   shapes/voxel/<id>.json   VoxelShapeFile (free-form block shapes)

    [Serializable] public class UnlockRule
    {
        public string group;   // empty = open from the start
        public int cleared;
        public bool IsNone => string.IsNullOrEmpty(group);
    }

    [Serializable] public class GroupRef
    {
        public string id;
        public UnlockRule unlock = new UnlockRule();
    }

    [Serializable] public class GameRules
    {
        public int windowBack = 10, windowAhead = 10;
        public int hintsPerLevel = 3, hintPenalty = 1;
        // each undo costs a rewarded ad; a level allows this many
        public int undosPerLevel = 5;
        // 1 = a hint or an undo plays a rewarded ad first; 0 = free for everyone (closed test);
        // 2 = ads only on the devices listed in adsTesters. Switched from Level Lab
        public int adsOn = 1;
        // device codes (settings card, seven taps on the version line) that see ads while adsOn is 2; commas or spaces between
        public string adsTesters = "";
        // installed version codes below this get the full-screen Play update at start (0 = never forced)
        public int updateBelow = 0;
        public int minScore = 3, parScore = 6, overParScore = 5;
        // wasted moves (past the fewest taps to the finished board) a win may have and still count as par (3 stars, Expert)
        public int wasteAllowed = 3;
        // the old par slack (par + percent, at least min): only app versions before the wasted-moves rule read it
        public int expertSlackPercent = 15, expertSlackMin = 0;
    }

    [Serializable] public class ManifestData
    {
        public int schema = 3;
        public string minApp = "0.1.0";
        public int shapesVersion = 1;
        public GameRules rules = new GameRules();
        public List<GroupRef> groups = new List<GroupRef>();
        /// <summary>Publish number from Level Lab (content-v&lt;n&gt; tag in the repo).</summary>
        public int contentVersion;
        /// <summary>Where the files of this publish are served (an immutable tag on jsDelivr); empty = next to the manifest.</summary>
        public string cdn = "";
        /// <summary>The weekly challenge now open (empty id = none). Older apps ignore it.</summary>
        public ChallengeWeek challenge = new ChallengeWeek();
    }

    /// <summary>
    /// One weekly challenge: a set of <see cref="count"/> levels per shape, easiest first, ranked by fewest moves and then
    /// time. A player joins the sets of the shapes they have unlocked. Level files sit at
    /// challenge/&lt;id&gt;/&lt;group&gt;/&lt;slot&gt;.json (slot 1..count), next to the other files of the publish.
    /// </summary>
    [Serializable] public class ChallengeWeek
    {
        /// <summary>ISO week, e.g. "2026-W49"; also the version of the weekly boards it is ranked on.</summary>
        public string id = "";
        /// <summary>Opening time in UTC, ISO 8601 ("2026-12-03T00:00:00Z"); it stays open for <see cref="days"/>.</summary>
        public string start = "";
        public int days = 7;
        /// <summary>Group ids (shapes) that have a set this week.</summary>
        public List<string> groups = new List<string>();
        /// <summary>Levels per shape (files 1..count), easiest first.</summary>
        public int count = 3;
        public bool IsNone => string.IsNullOrEmpty(id);
        public bool Has(string group) => !IsNone && groups != null && groups.Contains(group);
        public static string LevelPath(string id, string group, int slot) => "challenge/" + id + "/" + group + "/" + slot + ".json";
    }

    [Serializable] public class ChunkInfo
    {
        public int index, version, count;
    }

    [Serializable] public class GroupData
    {
        public string id, name, blurb, icon;
        public bool endless, enable;
        public int levelCount;
        public int chunkSize = 100;
        // this group's own wasted moves allowed (see GameRules); -1 = use the manifest rule
        public int wasteAllowed = -1;
        // this group's own old par slack, read only by app versions before the wasted-moves rule; -1 = the manifest rule
        public int expertSlackPercent = -1, expertSlackMin = -1;
        public List<ChunkInfo> chunks = new List<ChunkInfo>();
    }

    [Serializable] public class OrderChunk
    {
        public string group;
        public int chunk, version;
        public List<int> levels = new List<int>();
        /// <summary>Revision of each level in <see cref="levels"/> (same order); a newer one is downloaded again.</summary>
        public List<int> revs = new List<int>();
    }

    [Serializable] public class GenInfo
    {
        public int ver;            // 0 = drawn by hand
        public long seed;
        public List<int> colors = new List<int>();
        public float scramble;
        public int minPar;
    }

    [Serializable] public class LevelData
    {
        public string id;
        public int serial;
        public string group;
        public int rev;
        public string shape;
        public int ports;
        public string tiles;
        public List<int> locked = new List<int>();
        public int par;
        public string check;
        public float difficulty;
        public List<string> tags = new List<string>();
        public GenInfo gen = new GenInfo();
        public List<int> hintOrder = new List<int>();
    }

    /// <summary>A free-form shape from Level Lab: unit block positions, flattened x0,y0,z0,x1,y1,z1…</summary>
    [Serializable] public class VoxelShapeFile
    {
        public string id;
        public string type = "voxel";
        public List<int> blocks = new List<int>();
    }

    // shapes.json: one list per shape type (only for tools and documentation; the game reads shape ids directly).
    [Serializable] public class NShapeDef { public string id; public int n; }
    [Serializable] public class BallShapeDef { public string id; public float r; }
    [Serializable] public class CrossShapeDef { public string id; public int arm, thick; }
    [Serializable] public class TowerShapeDef { public string id; public int w, h; }
    [Serializable] public class EllShapeDef { public string id; public int len, thick; }
    [Serializable] public class ShapeCatalogFile
    {
        public int version;
        public List<NShapeDef> cube = new List<NShapeDef>();
        public List<NShapeDef> tetra = new List<NShapeDef>();
        /// <summary>Octahedra: n x n triangles per face.</summary>
        public List<NShapeDef> octa = new List<NShapeDef>();
        /// <summary>Dodecahedra: n = 2 Kilominx (60 kites), 3 Megaminx (132 tiles).</summary>
        public List<NShapeDef> dodeca = new List<NShapeDef>();
        /// <summary>Balls of pentagons and hexagons: n = 1 soccer ball (32), 2..4 Goldberg GP(n,0).</summary>
        public List<NShapeDef> geo = new List<NShapeDef>();
        public List<BallShapeDef> ball = new List<BallShapeDef>();
        public List<CrossShapeDef> cross = new List<CrossShapeDef>();
        public List<TowerShapeDef> tower = new List<TowerShapeDef>();
        public List<EllShapeDef> ell = new List<EllShapeDef>();
    }
}
