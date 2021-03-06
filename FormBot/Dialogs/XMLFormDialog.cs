﻿using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Xml.Linq;
using System.Linq;
using FormBot.Evangelism;
using System.Collections.Generic;
using System.Threading;
using FormBot.Evangelism.Data;

namespace FormBot.Dialogs
{

    [Serializable]
    public class XMLFormDialog<T> : IDialog<T> where T: Indexed, new()
    {
        Field[] Fields { get; }
        Field CurrentField { get; set; }
        T Object { get; set; }
        string Id { get; set; }

        protected IStore<T> Store { get; set; }

        string welcome_msg, return_msg;
        public XMLFormDialog(IStore<T> Store, string fname=null)
        {
            this.Store = Store;
            if (fname==null) fname = typeof(T).Name;
            var xdoc = XDocument.Load(System.Web.HttpContext.Current.Request.MapPath($"~/XML/{fname}.xml"));
            Fields = (from z in xdoc.Descendants("Field") select new Field(z)).ToArray();
            welcome_msg = xdoc.Descendants("Intro").First().Value;
            return_msg = xdoc.Descendants("WelcomeBack").First().Value;
        }

        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            string msg;
            var o = await result;
            if (o is Activity) // got activity
            {
                msg = ((Activity)o).Text;
                Id = ((Activity)o).From.Id;
            }
            else
            {
                msg = (string)o;
            }

            if (Object==null)
            {
                if (Store.Exists(Id))
                {
                    await context.PostAsync(return_msg);
                }
                else
                {
                    await context.PostAsync(welcome_msg);
                }
                Object = Store.Get(Id);
            }

            if (CurrentField!=null)
            {
                var val = msg;
                if (CurrentField.Set(Object, val))
                {
                    CurrentField = null;
                    Store.Update(Id,Object);
                }
                else await context.PostAsync("Ошибочное значение");
            }

            if (CurrentField == null)
            {
                foreach (var x in Fields)
                {
                    if (x.Get(Object) == null && x.IsApplicable(Object))
                    {
                        CurrentField = x;
                        break;
                    }
                }
            }
            if (CurrentField == null) // all fields of the object have been filled
            {
                // await context.Forward(new MenuDialog<T>(Store), async (ctx,x) => { ctx.Done(Object); }, activity, CancellationToken.None);
                context.Call(new MenuDialog<T>(Store,Object), async (ctx, c) => { ctx.Done(Object); });    
                // context.Done(Object);
            }
            else // we have a new field to fill
            {
                if (CurrentField.Type == "geostring")
                {
                    await context.PostAsync(CurrentField.Text);
                    context.Call(new GeoDialog("GeoCities"), MessageReceivedAsync);
                }
                else
                {
                    await CurrentField.Render(context);
                    context.Wait(MessageReceivedAsync);
                }
            }
        }
    }
}