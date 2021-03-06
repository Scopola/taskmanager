﻿using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace Common.TestAutomation.Framework.Pages
{
    public abstract class PageBase
    {
        protected readonly Uri PageUrl;
        protected readonly IWebDriver Driver;
        protected readonly WebDriverWait Wait;

        protected PageBase(IWebDriver driver, WebDriverWait wait, Uri pageUrl)
        {
            Driver = driver;
            Wait = wait;
            PageUrl = pageUrl;
        }

        public virtual void NavigateTo()
        {
            Driver.Navigate().GoToUrl(PageUrl);
        }

        public abstract bool HasLoaded { get; }
        
        protected void EnterTextInField(string textToEnter, Func<IWebElement> elementFunc)
        {
            IWebElement element = null;

            Wait.Until(d =>
            {
                try
                {
                    element = elementFunc.Invoke();
                    return element.Displayed;
                }
                catch (NoSuchElementException)
                {
                    return false;
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
            });

            element.Clear();
            element.SendKeys(textToEnter);
        }
    }
}