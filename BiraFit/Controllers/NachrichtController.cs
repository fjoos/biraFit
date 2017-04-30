﻿using System;
using BiraFit.Models;
using Microsoft.AspNet.Identity;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using BiraFit.ViewModel;
using BiraFit.Controllers.Helpers;
using NUnit.Framework;

namespace BiraFit.Controllers
{
    public class NachrichtController : BaseController
    {
        private int _id;
        private Sportler _sportler;
        private PersonalTrainer _personalTrainer;



        // GET: Nachricht
        public ActionResult Index()
        {
            _sportler = AuthentificationHelper.AuthenticateSportler(User, Context);
            if (_sportler == null)
            {
                _personalTrainer = AuthentificationHelper.AuthenticatePersonalTrainer(User, Context);
            }
            _id = _sportler != null ? _sportler.Id : _personalTrainer.Id;

            if (_sportler != null)
            {

                if (_id > 0)
                {
                    var sportlerKonversationen = from b in Context.Konversation
                        where b.Sportler_Id == _id
                        select b;
                    List<Konversation> sportlerKonversationList = sportlerKonversationen.ToList();
                    return View(sportlerKonversationList);
                }

            }

            var trainerKonversationen = from b in Context.Konversation
                where b.PersonalTrainer_Id == _id
                select b;
            List<Konversation> trainerKonversationList = trainerKonversationen.ToList();
            return View(trainerKonversationList);
        }

        public ActionResult Chat(int id)
        {

            if (!IsLoggedIn())
            {
                return RedirectToAction("Index", "Home");
            }

            var chat = from k in Context.Konversation
                where k.Id == id
                from m in k.Nachrichten
                orderby m.Datum
                select m;

            //List<Nachricht> chatList = Context.Nachricht.Where(i => i.Konversation_Id == id).OrderBy(d => d.Datum).ToList();

            List<Nachricht> chatList = chat.ToList();

            return View(new ChatViewModel { Nachrichten = chatList, KonversationId = id, Id = User.Identity.GetUserId() });
        }

        [HttpPost]
        public ActionResult SendMessage(ChatViewModel message)
        {
            var konversation = Context.Konversation.First(i => i.Id == message.KonversationId);

            string empfaengerId = User.Identity.GetUserId() == GetTrainerAspNetUserId(konversation.PersonalTrainer_Id) ? GetSportlerAspNetUserId(konversation.Sportler_Id) : GetTrainerAspNetUserId(konversation.PersonalTrainer_Id);
           
            /* funktioniert nicht ganz wegen Konversation_Id1
            Nachricht nachricht = new Nachricht()
            {
                Datum = DateTime.Now,
                Empfaenger_Id = empfaengerId,
                Sender_Id = User.Identity.GetUserId(),
                Text = message.Nachricht,
                Konversation_Id = message.KonversationId,
                
            };
            Context.Nachricht.Add(nachricht); */

            string query =
                $"INSERT INTO Nachricht (Text,Sender_Id,Empfaenger_Id,Datum,Konversation_Id,Konversation_Id1) VALUES ('{message.Nachricht}','{User.Identity.GetUserId()}',{empfaengerId},'{DateTime.Now}',{message.KonversationId},{message.KonversationId})";
            Context.Database.ExecuteSqlCommand(query);

            
            Context.SaveChanges();

            return RedirectToAction("Chat/" + message.KonversationId, "Nachricht");
        }



    }
}