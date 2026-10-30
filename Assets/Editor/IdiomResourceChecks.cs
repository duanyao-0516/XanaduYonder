using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XanaduYonder.Core.Idiom;
using XanaduYonder.Runtime.Idiom;

namespace XanaduYonder.EditorTools
{
    public static class IdiomResourceChecks
    {
        private static IEnumerator loading;
        private static AsyncOperation pending;
        private static IdiomCatalogLoader loader;
        private static IdiomLexicon lexicon;
        private static string failure;
        private static double started;

        // Run without -quit: the real asynchronous Resources loader needs editor ticks.
        public static void Run()
        {
            loader = new IdiomCatalogLoader();
            loading = loader.Load(value => lexicon = value, value => failure = value);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 120) throw new TimeoutException("Dictionary load timeout");
                if (pending != null && !pending.isDone) return;
                if (loading.MoveNext()) { pending = loading.Current as AsyncOperation; return; }
                EditorApplication.update -= Tick;
                if (failure != null || lexicon == null) throw new InvalidOperationException(failure ?? "No lexicon");
                var count = 0;
                foreach (var entry in lexicon.Entries)
                {
                    IdiomDefinition definition;
                    string error;
                    if (!loader.TryGetDefinition(entry.Id, out definition, out error)) throw new InvalidOperationException(error);
                    if (definition.SenseId != entry.SenseId || definition.Word != entry.Word || definition.Pinyin != entry.Pinyin)
                        throw new InvalidOperationException("Mismatched definition: " + entry.Word);
                    count++;
                }
                if (count != 45776) throw new InvalidOperationException("Unexpected packed count " + count);
                for (var match = 0; match < 2; match++)
                for (var mode = 0; mode < 2; mode++)
                for (uint seed = 1; seed <= 20; seed++)
                {
                    var profile = new IdiomLearningProfile(lexicon);
                    Func<long> clock = () => 1700000000L;
                    var game = new IdiomGame(lexicon, (IdiomMode)mode, seed, (IdiomMatchKind)match, profile, clock);
                    if (!game.Start(seed % 2 == 0 ? IdiomActor.App : IdiomActor.Player).Success)
                        throw new InvalidOperationException("Start failed");
                    while (!game.IsEnded && game.History.Count < 100)
                    {
                        var hint = game.RequestHint();
                        if (game.IsEnded) break;
                        IdiomEntry next = null;
                        if (hint != null && hint.EntryIds.Count > 0) lexicon.TryGetById(hint.EntryIds[0], out next);
                        else
                        {
                            var used = new HashSet<string>(game.History);
                            foreach (var entry in lexicon.Entries)
                            {
                                bool matches = game.LastEntry == null;
                                if (mode == 0) matches |= entry.FirstCharacter == game.LastEntry?.LastCharacter;
                                else
                                {
                                    matches = game.LastEntry == null && entry.SoundReadings.Count > 0;
                                    foreach (var reading in entry.SoundReadings)
                                        foreach (string key in game.CurrentSoundKeys) matches |= reading.FirstKey == key;
                                }
                                if (!used.Contains(entry.Id) && matches) { next = entry; break; }
                            }
                        }
                        if (next == null || !game.Submit(next.Word).Success)
                            throw new InvalidOperationException("Real dictionary continuation failed");
                        game = new IdiomGame(lexicon, game.GetSnapshot(), profile, clock);
                    }
                    new IdiomGame(lexicon, game.GetSnapshot(), profile, clock);
                }
                Debug.Log("IDIOM_RESOURCE_CHECKS_PASS: " + count + " exact definitions; 80 real dictionary bounded games/restore across both match kinds; version=" + lexicon.Version);
                loader.ClearDetails();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                EditorApplication.update -= Tick;
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
