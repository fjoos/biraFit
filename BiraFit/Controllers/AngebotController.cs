﻿using System;
using BiraFit.Models;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using BiraFit.ViewModel;
using Microsoft.AspNet.Identity;
using System.Net.Mail;

namespace BiraFit.Controllers
{
    public class AngebotController : BaseController
    {
        //
        // GET: /Angebot/Index
        public ActionResult Index()
        {
            if (IsSportler())
            {            
                var currentSportlerId = GetAspNetSpecificIdFromUserId(User.Identity.GetUserId());
                var currentBedarf = Context.Bedarf.FirstOrDefault(s => s.Sportler_Id == currentSportlerId);
                var userId = User.Identity.GetUserId();
                var user = Context.Users.Single(s => s.Id == userId);

                List<AngebotViewModel> angebotList = new List<AngebotViewModel>();
                    if (currentBedarf != null && currentBedarf.OpenBedarf)
                    {
                    var angebote = Context.Angebot.Where(b => b.Bedarf_Id == currentBedarf.Id).ToList();
                        foreach (var angebot in angebote)
                        {
                            var trainerId = GetAspNetUserIdFromTrainerId(angebot.PersonalTrainer_Id);
                            var trainer = Context.Users.Single(s => s.Id == trainerId);

                            angebotList.Add(new AngebotViewModel()
                            {
                                Angebot = angebot,
                                Bedarf = currentBedarf,
                                peronalTrainerId = trainer.Id,
                                trainerUsername = trainer.Email,
                                sportlerPicture = user.ProfilBild,
                                trainerPicture = trainer.ProfilBild
                            });
                        }
                        return View(angebotList);
                    }
                
            }
            return RedirectToAction("Index", "Home");
        }

        //
        // POST: /Angebot/create
        [HttpPost]
        public ActionResult Create(BedarfViewModel bedarfviewmodel)
        {
            if (ModelState.IsValid)
            {
                Context.Angebot.Add(new Angebot()
                {
                    Beschreibung = bedarfviewmodel.Beschreibung,
                    Datum = DateTime.Now,
                    PersonalTrainer_Id = GetAspNetSpecificIdFromUserId(User.Identity.GetUserId()),
                    Preis = bedarfviewmodel.Preis,
                    Bedarf_Id = bedarfviewmodel.Id
                });
                Context.SaveChanges();
                return RedirectToAction("Index", "Home");
            }
            return RedirectToAction("Index", "Home");
        }

        //
        // GET: /Angebot/Accept/<id>
        public ActionResult Accept(int id)
        {
            if (IsSportler() && SportlerHasAngebot(id))
            {

                var currentAngebot = Context.Angebot.Single(i => i.Id == id);
                var sportlerId = GetAspNetSpecificIdFromUserId(User.Identity.GetUserId());
                var sportlerUserId = GetAspNetUserIdFromSportlerId(sportlerId);
                var personalTrainerId = currentAngebot.PersonalTrainer_Id;
                var personalTrainerUserId = GetAspNetUserIdFromTrainerId(personalTrainerId);
                var peronalTrainerEmail = Context.Users.Single(s => s.Id == personalTrainerUserId).Email;
                var sportlerEmail = Context.Users.Single(s => s.Id == sportlerUserId).Email;
                var bedarfId = Context.Bedarf.Single(i => i.Sportler_Id == sportlerId && i.OpenBedarf);

                var angeboteToRemove = Context.Angebot.Where(i => i.Bedarf_Id == currentAngebot.Bedarf_Id).ToList();
                var canceledAngebote = Context.Angebot.Where(i => i.Id != id && i.Bedarf_Id == currentAngebot.Bedarf_Id).ToList();

                createEmailForPersonaTrainer(peronalTrainerEmail);
                createEmailForSportler(sportlerEmail);
                createCancelEmails(canceledAngebote);
                startConversation(personalTrainerId, sportlerId);


                Context.Angebot.RemoveRange(angeboteToRemove);
                Context.Bedarf.Remove(bedarfId);
                Context.SaveChanges();
                return RedirectToAction("Chat/" + Context.Konversation.Single(i => i.Sportler_Id == sportlerId && i.PersonalTrainer_Id == personalTrainerId).Id, "Nachricht");
            }
            return RedirectToAction("Index", "Home");

        }

        //
        // GET:/Angebot/Reject/<id> Angebot as a Sportler
        public ActionResult Reject(int id)
        {
            if (IsSportler() && SportlerHasAngebot(id))
            {
                var angebot = Context.Angebot.Single(i => i.Id == id);
                var message = "Tut uns Leid Ihr Angebot wurde abgelehnt. Versuchen Sie es weiter!";
                var persUserId = GetAspNetUserIdFromTrainerId(angebot.PersonalTrainer_Id);
                var email = Context.Users.Single(s => s.Id == persUserId).Email; 
                SendMail(email, message, "Ihr Angebot wurde abgelehnt");


                Context.Angebot.Remove(angebot);
                Context.SaveChanges();

                return RedirectToAction("Index", "Angebot");
            }
            return RedirectToAction("Index", "Home");

        }

        //
        // GET: /Angebot/Withdraw/<id> (delete) as a Trainer
        public ActionResult Withdraw(int id)
        {
            if (IsSportler() || !IsLoggedIn())
            {
                return RedirectToAction("Index", "Home");
            }
            var personalTrainerId = GetAspNetSpecificIdFromUserId(User.Identity.GetUserId());
            var offerToRemove = Context.Angebot.Single(
                i => i.Bedarf_Id == id && i.PersonalTrainer_Id == personalTrainerId);
            Context.Angebot.Remove(offerToRemove);
            Context.SaveChanges();
            return RedirectToAction("AngebotTrainer", "Angebot");
        }

        //
        // GET: /Angebot/AngebotList
        public ActionResult AngebotTrainer()
        {
            if (IsSportler() || !IsLoggedIn())
            {
                return RedirectToAction("Index", "Home");
            }
            var currentPersonalTrainerId = GetAspNetSpecificIdFromUserId(User.Identity.GetUserId());
            var currentAngebot = Context.Angebot.Where(s => s.PersonalTrainer_Id == currentPersonalTrainerId).ToList();
            List<AngebotViewModel> angebotList = new List<AngebotViewModel>();
            foreach (var angebot in currentAngebot)
            {
                var bedarf = Context.Bedarf.FirstOrDefault(b => b.Id == angebot.Bedarf_Id);
                    if (bedarf != null && bedarf.OpenBedarf)
                    {
                        var sportlerId = GetAspNetUserIdFromSportlerId(bedarf.Sportler_Id);
                        var sportler = Context.Users.Single(s => s.Id == sportlerId);
                        angebotList.Add(new AngebotViewModel()
                        {
                            Angebot = angebot,
                            Bedarf = bedarf,
                            sportlerPicture = sportler.ProfilBild,
                            sportlerUsername = sportler.Email
                        });
                    }
                
            }
            return View(angebotList);
        }

        private void startConversation(int trainerId, int sportlerId)
        {
            if (!Context.Konversation.Any(s => s.PersonalTrainer_Id == trainerId && s.Sportler_Id == sportlerId))
            {
                Context.Konversation.Add(new Konversation()
                {
                    Sportler_Id = sportlerId,
                    PersonalTrainer_Id = trainerId,
                    SportlerDeleted = false,
                    TrainerDeleted = false
                });
            }
        }

        private void createEmailForPersonaTrainer(string email)
        {
            var message = "Ihr Angebot wurde angenommen. Eine neue Konversation ist nun verfügbar.";
            var subject = "Ihr Angebot wurde angenommen";
            SendMail(email, message, subject);
        }

        private void createEmailForSportler(string email)
        {
            var message = "Sie haben ein Angebot angenommen. Eine neue Konversation ist nun verfügbar.";
            var subject = "Sie haben ein Angebot angenommen";
            SendMail(email, message, subject);
        }

        private void createCancelEmails(List<Angebot> receiverList)
        {
            var massage = "Tut uns leid, ihr Angebot wurde leider abgelehnt. Versuchen Sie es weiter!";
            var subject = "Ihr Angebot wurde leider abgelehnt";
            foreach (Angebot cancelAngebot in receiverList)
            {
                var cancelPersonalTrainer = GetAspNetUserIdFromTrainerId(cancelAngebot.PersonalTrainer_Id);
                var receiverEmail = Context.Users.Single(s => s.Id == cancelPersonalTrainer).Email;                
                SendMail(receiverEmail, massage, subject);
            }
        }

        private void SendMail(string email, string message, string subject)
        {
            MailMessage msg = new MailMessage();
            SmtpClient client = new SmtpClient();
            try
            {
                msg.Subject = subject;
                msg.Body = message;
                msg.From = new MailAddress("birafit17@gmail.com");
                msg.To.Add(email);
                msg.IsBodyHtml = true;
                client.Host = "smtp.gmail.com";
                System.Net.NetworkCredential basicauthenticationinfo = new System.Net.NetworkCredential("birafit17@gmail.com", "Hsr-12345");
                client.Port = int.Parse("587");
                client.EnableSsl = true;
                client.UseDefaultCredentials = false;
                client.Credentials = basicauthenticationinfo;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Send(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public bool SportlerHasAngebot(int angebotId)
        {
            var currentAngebot = Context.Angebot.Single(i => i.Id == angebotId);
            var sportlerId = GetAspNetSpecificIdFromUserId(User.Identity.GetUserId());
            if (Context.Bedarf.Any(i => i.Sportler_Id == sportlerId))
            {
                return Context.Bedarf.Single(i => i.Sportler_Id == sportlerId && i.OpenBedarf).Id ==
                   currentAngebot.Bedarf_Id;
            }
            return false;
        }

        [ChildActionOnly]
        public ActionResult angebotNavigation()
        {
            if (IsSportler())
            {
                ViewBag.Type = "Sportler";
            }
            else
            {
                ViewBag.Type = "Personaltrainer";
            }
            return PartialView();
        }
    }
}