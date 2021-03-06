﻿using System;
using BiraFit.Models;
using Microsoft.AspNet.Identity;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using BiraFit.ViewModel;

namespace BiraFit.Controllers
{
    public class NachrichtController : BaseController
    {
        //
        // GET: Nachricht
        public ActionResult Index()
        {
            List<Konversation> konversationList;
            int specificId = GetAspNetSpecificIdFromUserId(User.Identity.GetUserId());
            NachrichtViewModel result = new NachrichtViewModel();

            if (IsSportler())
            {
                konversationList = Context.Konversation.Where(k => k.Sportler_Id == specificId && !k.SportlerDeleted).ToList();
                result = GetViewModelBySportler(konversationList);
            }
            else
            {
                konversationList = Context.Konversation.Where(k => k.PersonalTrainer_Id == specificId && !k.TrainerDeleted).ToList();
                result = GetViewModelByTrainer(konversationList);
            }

            return View(result);
        }

        //
        // GET: Nachricht/Delete/id
        public ActionResult Delete(int id)
        {
            Konversation konv = Context.Konversation.FirstOrDefault(i => i.Id == id);
            var currentUserId = GetAspNetSpecificIdFromUserId(User.Identity.GetUserId());
            if(konv != null && (konv.Sportler_Id == currentUserId || konv.PersonalTrainer_Id == currentUserId))
            {
                if (IsSportler())
                {
                    konv.SportlerDeleted = true;
                }
                else
                {
                    konv.TrainerDeleted = true;
                }
                Context.SaveChanges();
                if (konv.TrainerDeleted && konv.SportlerDeleted)
                {
                    DeleteMessages(konv.Id);
                    Context.Konversation.Remove(konv);
                    Context.SaveChanges();
                }
                return RedirectToAction("Index", "Nachricht");
            }
            return HttpNotFound();
        }

        //
        // GET: Nachricht/Chat/<id>
        public ActionResult Chat(int id)
        {
            if (CheckPermission(id))
            {
                var chat = from k in Context.Konversation
                           where k.Id == id
                           from m in k.Nachrichten
                           orderby m.Datum
                           select m;
                List<Nachricht> chatList = chat.ToList();
                var empfängerName = "";
                var empfängerId = "";
                var senderId = User.Identity.GetUserId();

                if (IsSportler())
                {
                    var personalTrainerId = Context.Konversation.Single(s => s.Id == id).PersonalTrainer_Id;
                    var personalTrainer = GetAspNetUserIdFromTrainerId(personalTrainerId);
                    empfängerName = Context.Users.Single(s => s.Id == personalTrainer).Email;
                    empfängerId = Context.Users.Single(s => s.Id == personalTrainer).Id;
                }
                else
                {
                    var sportlerId = Context.Konversation.Single(s => s.Id == id).Sportler_Id;
                    var sportler = GetAspNetUserIdFromSportlerId(sportlerId);
                    empfängerName = Context.Users.Single(s => s.Id == sportler).Email;
                    empfängerId = Context.Users.Single(s => s.Id == sportler).Id;
                }
                var senderBild = Context.Users.Single(u => u.Id == senderId).ProfilBild;
                var empfängerBild = Context.Users.Single(u => u.Id == empfängerId).ProfilBild;

                if (senderBild == null)
                {
                    senderBild = "standardprofilbild.jpg";
                }

                if (empfängerBild == null)
                {
                    empfängerBild = "standardprofilbild.jpg";
                }

                return View(new ChatViewModel
                {
                    Nachrichten = chatList,
                    KonversationId = id,
                    Id = User.Identity.GetUserId(),
                    Empfänger = empfängerName,
                    EmpfängerId = empfängerId,
                    IsSportler = IsSportler(),
                    SenderProfilBild = senderBild,
                    EmpfängerProfilBild = empfängerBild,
                });
            }
            return HttpNotFound();

        }

        //
        // POST: Nachricht/Chat/<id>
        [HttpPost]
        public ActionResult SendMessage(ChatViewModel message)
        {
            if (ModelState.IsValid)
            {
                var konversation = Context.Konversation.First(i => i.Id == message.KonversationId);
                konversation.SportlerDeleted = false;
                konversation.TrainerDeleted = false;
                Context.SaveChanges();
                string empfaengerId = User.Identity.GetUserId() == GetAspNetUserIdFromTrainerId(konversation.PersonalTrainer_Id)
                    ? GetAspNetUserIdFromSportlerId(konversation.Sportler_Id)
                    : GetAspNetUserIdFromTrainerId(konversation.PersonalTrainer_Id);
                string query =
                    $"INSERT INTO Nachricht (Text,Sender_Id,Empfaenger_Id,Datum,Konversation_Id,Konversation_Id1) VALUES ('{message.Nachricht}','{User.Identity.GetUserId()}','{empfaengerId}',CONVERT(datetime, '{DateTime.Now}', 104),{message.KonversationId},{message.KonversationId})";
                Context.Database.ExecuteSqlCommand(query);
                return RedirectToAction("Chat/" + message.KonversationId, "Nachricht");
            }
            return View("~/Views/Nachricht/Chat/" + message.KonversationId, message);
        }

        private bool CheckPermission(int konversationId)
        {
            if (!IsLoggedIn())
            {
                return false;
            }

            var konversation = GetKonversation(konversationId);

            if (IsSportler())
            {
                return konversation.Sportler_Id == GetAspNetSpecificIdFromUserId(User.Identity.GetUserId());
            }

            return konversation.PersonalTrainer_Id == GetAspNetSpecificIdFromUserId(User.Identity.GetUserId());
        }

        private NachrichtViewModel GetViewModelBySportler(List<Konversation> konversationList)
        {
            List<string> lastMessages = new List<string>();
            List<string> profileImages = new List<string>();
            List<string> userNames = new List<string>();
            List<DateTime> sendTimes = new List<DateTime>();
            foreach (var konversation in konversationList)
            {
                lastMessages.Add(GetLastMessage(konversation.Id));
                sendTimes.Add(GetLastTime(konversation.Id));
                string trainerId = GetAspNetUserIdFromTrainerId(konversation.PersonalTrainer_Id);
                string username = Context.Users.Single(i => i.Id == trainerId).Email;
                var profileImage = Context.Users.Single(i => i.Id == trainerId).ProfilBild ??
                                   "standardprofilbild.jpg";
                userNames.Add(username);
                profileImages.Add(profileImage);
            }
            return new NachrichtViewModel
            {
                Konversationen = konversationList,
                LastMessages = lastMessages,
                ProfileImages = profileImages,
                UserNames = userNames,
                SendTimes = sendTimes
            };
        }

        private NachrichtViewModel GetViewModelByTrainer(List<Konversation> konversationList)
        {
            List<string> lastMessages = new List<string>();
            List<string> profileImages = new List<string>();
            List<string> userNames = new List<string>();
            List<DateTime> sendTimes = new List<DateTime>();
            foreach (var konversation in konversationList)
            {
                lastMessages.Add(GetLastMessage(konversation.Id));
                sendTimes.Add(GetLastTime(konversation.Id));
                string sportlerId = GetAspNetUserIdFromSportlerId(konversation.Sportler_Id);
                string username = Context.Users.Single(i => i.Id == sportlerId).Name;
                var profileImage = Context.Users.Single(i => i.Id == sportlerId).ProfilBild ??
                                   "standardprofilbild.jpg";
                userNames.Add(username);
                profileImages.Add(profileImage);
            }
            return new NachrichtViewModel
            {
                Konversationen = konversationList,
                LastMessages = lastMessages,
                ProfileImages = profileImages,
                UserNames = userNames,
                SendTimes = sendTimes
            };
        }

        private void DeleteMessages(int id)
        {
            var openMessages = Context.Nachricht.Where((s => s.Konversation_Id == id));
            foreach (Nachricht item in openMessages)
            {
                Context.Nachricht.Remove(item);
            }
        }

        private Konversation GetKonversation(int konversationId)
        {
            return Context.Konversation.Single(k => k.Id == konversationId);
        }

        private string GetLastMessage(int konversationId)
        {
            var messages = Context.Nachricht.Where(n => n.Konversation_Id == konversationId).OrderBy(m => m.Datum);
            string lastMessage = "";
            foreach (var message in messages)
            {
                lastMessage = message.Text;
            }
            return lastMessage;
        }

        private DateTime GetLastTime(int konversationId)
        {
            var messages = Context.Nachricht.Where(n => n.Konversation_Id == konversationId).OrderBy(m => m.Datum);
            DateTime sendtime = new DateTime();
            foreach (var message in messages)
            {
                sendtime = message.Datum;
            }
            return sendtime;
        }
    }
}