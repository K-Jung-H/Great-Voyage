using System;
using GreatVoyage.Application;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreatVoyage.Presentation
{
    public sealed class CabinHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text clockLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private GameObject journal;
        [SerializeField] private GameObject menu;
        [SerializeField] private Button journalClose;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button resume;
        [SerializeField] private Button saveExit;
        private GameSession session;
        private Action exitAction;
        private long lastMinute = -1;
        private bool lastReady, lastNearExit, wasJournal, wasMenu;

        public void Bind(GameSession value, Action requestExit, string notice)
        {
            session = value; exitAction = requestExit;
            journalClose.onClick.AddListener(CloseJournal);
            menuButton.onClick.AddListener(OpenMenu);
            resume.onClick.AddListener(Resume);
            saveExit.onClick.AddListener(Exit);
            session.Changed += Refresh;
            messageLabel.text = notice;
            Refresh();
        }

        private void CloseJournal() => session.CloseJournal();
        private void OpenMenu() => session.OpenMenu();
        private void Resume() => session.CloseMenu();
        private void Exit() => exitAction();

        public void ShowStartupFailure(string message, Action quit)
        {
            journal.SetActive(false); menu.SetActive(true);
            resume.interactable = false;
            saveExit.GetComponentInChildren<TMP_Text>().text = "종료";
            saveExit.onClick.AddListener(() => quit());
            messageLabel.text = message;
            hintLabel.text = "";
        }

        private void Refresh()
        {
            hintLabel.text = "";
            journal.SetActive(session.JournalOpen);
            menu.SetActive(session.MenuOpen);
            journalClose.interactable = !session.MenuOpen;
            if (session.MenuOpen && !wasMenu) EventSystem.current?.SetSelectedGameObject(resume.gameObject);
            else if (session.JournalOpen && !wasJournal) EventSystem.current?.SetSelectedGameObject(journalClose.gameObject);
            else if (!session.MenuOpen && !session.JournalOpen) EventSystem.current?.SetSelectedGameObject(null);
            wasMenu = session.MenuOpen; wasJournal = session.JournalOpen;
            if (!string.IsNullOrEmpty(session.Error)) messageLabel.text = session.Error;
        }

        private void Update()
        {
            if (session == null) return;
            var minute = (long)(session.State.Clock.TotalGameSeconds / 60);
            if (minute != lastMinute)
            {
                var day = minute / 1440 + 1;
                clockLabel.text = $"Day {day}  {minute / 60 % 24:00}:{minute % 60:00}";
                lastMinute = minute;
            }
            var ready = session.CanInteract;
            var nearExit = session.State.Player.Position.Y < -2.35f && Mathf.Abs(session.State.Player.Position.X) < 1.2f;
            if (ready != lastReady || nearExit != lastNearExit || string.IsNullOrEmpty(hintLabel.text))
            {
                hintLabel.text = session.Pauses.IsPaused ? "" : ready ? "E  항해 일지 열기" : nearExit
                    ? "현재 출입구를 이용할 수 없습니다."
                    : "WASD / 방향키  이동     E  상호작용     Esc  메뉴";
                lastReady = ready;
                lastNearExit = nearExit;
            }
        }

        private void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
            journalClose.onClick.RemoveListener(CloseJournal);
            menuButton.onClick.RemoveListener(OpenMenu);
            resume.onClick.RemoveListener(Resume);
            saveExit.onClick.RemoveListener(Exit);
        }
    }
}
