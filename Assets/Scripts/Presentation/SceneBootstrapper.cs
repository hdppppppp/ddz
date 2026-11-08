using UnityEngine;

namespace DDZ.Presentation
{
    /// <summary>
    /// 引导：代码装配 GamePresenter + GameBoard 并开局（人类执座位 0）。
    /// </summary>
    public sealed class SceneBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            gameObject.AddComponent<GameBoard>();
            var presenter = gameObject.AddComponent<GamePresenter>();
            presenter.StartRound(humanPlays: true);
        }
    }
}
