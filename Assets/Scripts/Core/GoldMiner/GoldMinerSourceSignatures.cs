using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
namespace XanaduYonder.Core.GoldMiner
{
    // Generated from the hash-verified, unchanged 1205-object source JSON.
    internal static class GoldMinerSourceSignatures
    {
        static readonly string[] solo = {
            "620dd969e232f4d6a0cc802d7eba6dd428951705cf9da4d6794d290a416753f3",
            "04dd84d8f627d6784c95ccfad6b96963d5c4b32e8e6bb642caf5f75e10e86252",
            "015388d361918cb378e7662a99d4f8f4885fde6dbfae3993efa005a28efb09e7",
            "a9ea1c025d9b8dd665f0fd8aba7b77b61d717f0d6b93485a01d03226275087f4",
            "80f774f9c75352d4699146bf18c619876f77749149ffceb44fc270eb4c91bfc5",
            "83ef883c76eeb33be61a4d1b68270a1d89b8aa2664981a4e5e4a48d86ded5fde",
            "f3c33e822342619b23b5e28ec590ebd1ed43e5b8f5df0681c6394331d440aa5c",
            "6532ff3db0cbaacdc4177eb19ed0ed563319a868488f40d64fa3b1d447f9a397",
            "284e3af92b2b83c345d22d549a4258fb21b4e9bb2964bb1277be43fdd534dcdb",
            "001051dc57895a9e1b0fc561529e84df6158108cde7486071750d6aae4e0780f",
            "8bd08d58f333024a7529b4a7b2eebe1986f1ab51bc6bf1ca6f45179f3832b414",
            "47aa0af7d228d0b6f9892fda62c05242a5091a474f019db2a1adbc5f8a7aae41",
            "98d4a92ecc96e1ba17a8a70e74429c8d6581cd2e76482d3eb32afc451cda5775",
            "31c6a7ba977dee121807e52ef79b27b2ea486c3badada46069af80d234bea178",
            "f038d406f0add2544718869c32f331b1f622a5eb3780fb4c775277d31d26bad6",
            "53edf7e9e93bb650e632585c0ca2b4b2aadf4511e04bd2c22dfb8136508e81c6",
            "d8999275048497c1f2a1c99fad89af113e566a23b84f947b1ae495f5bbc1a3f6",
            "0361721b9a4e7bb88ca62638f263b0c3d60feb64a772f9f00954ffc35e0b44d8",
            "78a1149e2d117cb3061912958c3bde5971762c5063d7fbd3295eb99b8f58dfc1",
            "024997614ff610c4a5870a6ebb8d04483545656edde18b6cf3f63b10d2ea6c5a",
            "27991f3eca2006d0af589ec28140e02a3e5a74cea275dc6fab3f6c2454971bb0",
            "d408c67f1a2e6b53b291dabf87802e44cb5027c34da18d24742d56d1e209da81",
            "0acdab2b4c626189c1f3d8aaf16a26fe330abf6836a9994c2a6a1430401e3e16",
            "0950a591986227e7e81ffd50499d62eb1bd04510e47a12f06bde59e736101388",
            "f848deeedaf68064f74f0e1b21947959044f5d831d25ca0c5098821eddfc7519",
            "50d671b51c242a545cc96ce9a4fa940a1c438c4a64fbb10ddd87d978127a5550",
            "7e226fa7e54a19fa3c35fd2c9e38c681baa2a80790f6c754fb06283e462ded08",
            "8a32a33bb812aeb2af136d1008fb9c2ed3bf5627fa473d4ef06d4b4936d63ece",
            "7562e8f277d9edf6ef064567b510ffcdaabd22a96be98f364838bf832f966eb9",
            "aeda64763d173f2ff31e14c82ae966d0fc688aeae86ebf7f1e31500ff669df58"
        };
        static readonly string[] coop = {
            "06767a4248f406fd843f6823eff3e3b795dbaa49c5f62de6b7df08f4e2bcaa23",
            "c85556dd9c7d66bac96a70945b53b23757db64156f536fe12ae8538383eb2d86",
            "930830b4b6a388c934c76531dadb565bac8292a59a9d57bb9e31460a06e76fcc",
            "2ba6807416d56b10d7a3b21a13984d960fb9f031e9fe0a9ee4a4163f6a8b97b0",
            "b4422d4381ac5c2d2da7006200a85063a5953a003f2c2b2e4fd73e0b886f6370",
            "552e1673c5c8bfb171e5ccb7dc8e5bc1efc264ccce008e55da2c685fa339cb51",
            "9f7e874b32813fa8755f15a2890ad911463c1d8cb9108e6dc4fd2c3967801185",
            "90d0813284a44f4bcc35368d8e3b856f22a661c327737e41942e2dbc834967c5",
            "929ff04691d2464098ecc58d64b31efbed6069e64c900d7b0ce6eb934d811f44",
            "4619c77ffe98e1bfceb7a46abea3f7e245b6451c85fefca9102bc75a9432b4b4",
            "4573b6f9ccff4b0bf3fee5d8b5c44c051d36513f3185a0e1c795b0d35d80ef96",
            "5d2774408c087745d71d1496b10900d41fd7c38e8093738a19d26d76609baf42",
            "7209269853d7b85dc673d1ec3dc7c312f318263aee67f58e24184a5a7491bc39",
            "3a66ab6b046fb8a5224137661a18794ad598ecb35ab402165ba49262ed036813",
            "bb62e2cd4da1c0455503b66b46fd953f68c2e35586636e6fe62cd2b50d6f3c78",
            "0e7913d86fc73530c2c17f2c98932b65033654f6e30c2609cf1b8e507d73630b",
            "1ba5da0f2c299a250600b968e3005b4578e4447b9819789163f992ee47c2e828",
            "83ce06279f74cd09638fbf96df208d6e66b92df0c5c3fc135ff4b9908fb62984",
            "0abda1a13e628430d02b9df9aabd4ebaf7f00d5977248b7e2d1f807308a12594",
            "df4bb82c1b89bd2ced503d01444d00d8dab92ea06361c0db98c1b4b73ae5a538",
            "e8a54a01dd628d1d1e443d35fa3b66b8272abe68e9b2996cb1a3f4ef86797982",
            "8c47beeb449d91e8891fcbbcfec4bb722e9d87bdc655c9c384665e619cb1ae71",
            "99708d7bdb900f9197c2e927810553b6c2115677e1462ffdfaf0abcefb156a22",
            "94374ddeb5f979d8d401787c081c020da071f6143fb673c99629acf3d0f9a64e",
            "2276f8a33653ae08f2e2a339de9e494036f724a89c772581c6059752c2b55d77",
            "48d7482bdfa5389965b9683015c93ab679b7a657c434b92f45722df1e96528f0",
            "27d7ba8b8a79b89e42d570fb6c897d729865af0ffbc0fe5a4a3179e73e051a9c",
            "f3e067327ba5d3ce0ca6269a1c52c31b14edc4b7be83d641756e36010f937972",
            "812ce3c9633b38182c37226ed5c1f0bdbc0b68ec13dcfa7744e566ebdbdac876",
            "9491616399ff40b973bd7212e14c566ffb712869e2668de714d9588c1e9b398c"
        };
        internal static bool Matches(GoldMinerMode mode, int map, GoldMinerEntity[] entities)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8)) {
                foreach(var e in entities) {var d=e.Source;writer.Write(Encoding.UTF8.GetBytes(d.Key+"\0"));
                    foreach(double n in new double[] {d.Type,(int)e.Kind,d.InitialX,d.InitialY,d.ScaleX,d.ScaleY,d.Flip?1:0,d.MotionEndX,d.Radius,d.Type==12?0:d.Value,d.Type==12?0:d.Weight})writer.Write(n);
                }
                using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant()==(mode==GoldMinerMode.Solo?solo:coop)[map];
            }
        }
    }
}
