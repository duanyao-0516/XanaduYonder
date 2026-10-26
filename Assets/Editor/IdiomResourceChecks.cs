using System;
using System.Collections;
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
                if (count != 42851) throw new InvalidOperationException("Unexpected packed count " + count);
                for (var mode = 0; mode < 2; mode++)
                for (uint seed = 1; seed <= 20; seed++)
                {
                    var game = new IdiomGame(lexicon, (IdiomMode)mode, seed);
                    if (!game.Start(seed % 2 == 0 ? IdiomActor.App : IdiomActor.Player).Success)
                        throw new InvalidOperationException("Start failed");
                    while (!game.IsEnded)
                    {
                        var hint = game.RequestHint();
                        if (game.IsEnded) break;
                        if (hint == null || hint.EntryIds.Count == 0) throw new InvalidOperationException("No hint");
                        IdiomEntry next;
                        if (!lexicon.TryGetById(hint.EntryIds[0], out next) || !game.Submit(next.Word).Success)
                            throw new InvalidOperationException("Real dictionary continuation failed");
                        game = new IdiomGame(lexicon, game.GetSnapshot());
                    }
                    new IdiomGame(lexicon, game.GetSnapshot());
                }
                Debug.Log("IDIOM_RESOURCE_CHECKS_PASS: " + count + " exact definitions; 40 real dictionary games/restore; version=" + lexicon.Version);
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
