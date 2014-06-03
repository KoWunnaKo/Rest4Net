﻿using System;
using System.Collections.Generic;
using System.Json;
using Rest4Net.ePochta.Responses;
using Rest4Net.ePochta.Responses.Implementation;
using Rest4Net.ePochta.Utils;
using Rest4Net.Exceptions;
using Rest4Net.Protocols;

namespace Rest4Net.ePochta
{
    /// <summary>
    /// This provider allows to access ePochta API through REST API as described here:
    /// http://www.epochta.ru/products/sms/v3.php
    /// </summary>
    public class ePochtaProvider : RestApiProvider
    {
        private readonly string _apiPublicKey;

        public ePochtaProvider(string apiPublicKey, string apiPrivateKey, bool useHttps = false)
            : base(
                useHttps
                    ? (BaseProtocol) new AtomParkProtocolHttps(@"atompark.com") {PrivateKey = apiPrivateKey}
                    : new AtomParkProtocolHttp(@"atompark.com") {PrivateKey = apiPrivateKey})
        {
            _apiPublicKey = apiPublicKey;
        }

        private Command Run(string methodName)
        {
            return Cmd("/api/sms/3.0/" + methodName, RequestType.Post)
                .WithParameter("version", "3.0")
                .WithParameter("action", methodName)
                .WithParameter("key", _apiPublicKey);
        }

        private static JsonValue CheckForError(JsonValue arg)
        {
            if (arg == null || !arg.ContainsKey("error"))
                return arg;
            throw new ResultException(arg["error"].ReadAs<string>(), arg["code"].ReadAs<int>(), arg);
        }

        /// <summary>
        /// Create new address book
        /// </summary>
        /// <param name="name">Name for the new address book</param>
        /// <param name="description">Optional value for address book description</param>
        /// <returns>Id for the newly created address book</returns>
        public int CreateAddressbook(string name, string description = null)
        {
            return
                Run("addAddressbook")
                    .WithParameter("name", name)
                    .WithParameterIfNotNullOrWhiteSpace("description", description)
                    .Execute()
                    .To<ResponsePureImpl<AddressbookCreateImpl>>(CheckForError)
                    .result.Id;
        }

        /// <summary>
        /// Edit address book
        /// </summary>
        /// <param name="id">Id of the address book to edit</param>
        /// <param name="newName">New name for the address book</param>
        /// <param name="newDescription">New description for the address book</param>
        /// <returns></returns>
        public bool EditAddressbook(int id, string newName, string newDescription = null)
        {
            return Run("editAddressbook")
                .WithParameter("idAddressBook", id)
                .WithParameter("newName", newName)
                .WithParameterIfNotNullOrWhiteSpace("newDescr", newDescription)
                .Execute()
                .To<SuccessResultImpl>(CheckForError)
                .result.successful;
        }

        /// <summary>
        /// Delete address book
        /// </summary>
        /// <param name="id">Id for the address book to delete</param>
        /// <returns>Operation success</returns>
        public bool DeleteAddressbook(int id)
        {
            return
                Run("delAddressbook")
                    .WithParameter("idAddressBook", id)
                    .Execute()
                    .To<SuccessResultImpl>(CheckForError)
                    .result.successful;
        }

        /// <summary>
        /// Fetch full information about one address book
        /// </summary>
        /// <param name="id">Id for the address book to get</param>
        /// <returns>Full information about address book</returns>
        public IAddressBook GetAddressbook(int id)
        {
            return
                Run("getAddressbook")
                    .WithParameter("idAddressBook", id)
                    .Execute()
                    .To<ResponseImpl<IAddressBook, AddressBookImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// Fetch full information about many address books
        /// </summary>
        /// <param name="offset">Array offset to get elements</param>
        /// <param name="count">Count of elements to fetch from server</param>
        /// <returns>Full information of many address books</returns>
        public IAddressBooks ListAddressbooks(uint offset = 0, uint count = 50)
        {
            return Run("getAddressbook")
                .WithParameter("from", offset)
                .WithParameter("offset", count)
                .Execute()
                .To<ResponseImpl<IAddressBooks, AddressBooksImpl>>(RemakeJsonForList)
                .result;
        }

        private static JsonValue RemakeJsonForList(JsonValue arg)
        {
            var oInitial = CheckForError(arg);
            if (oInitial == null || !oInitial.ContainsKey("result"))
                return oInitial;
            var o = oInitial["result"];
            if (o == null || !o.ContainsKey("fields") || !o.ContainsKey("data"))
                return o;
            var fieldsList = new List<string>();
            var arr = (JsonArray)(o["fields"]);
            foreach (var item in arr)
                fieldsList.Add(item.ReadAs<string>());
            arr = (JsonArray)(o["data"]);
            var result = new JsonArray();
            foreach (var item in arr)
            {
                var itm = (JsonArray) item;
                var cnt = itm.Count;
                var obj = new JsonObject();
                for (var i = 0; i < cnt; i++)
                    obj.SetValue(fieldsList[i], itm[i]);
                result.Add(obj);
            }
            o.SetValue("items", result);
            return oInitial;
        }

        /// <summary>
        /// Clone address book
        /// </summary>
        /// <param name="id">If for the address book to clone</param>
        /// <returns>Id for the cloned address book</returns>
        public int CloneAddressbook(int id)
        {
            return
                Run("cloneaddressbook")
                    .WithParameter("idAddressBook", id)
                    .Execute()
                    .To<ResponsePureImpl<AddressbookCloneImpl>>(CheckForError)
                    .result.Id;
        }

        /// <summary>
        /// Add new phone to address book
        /// </summary>
        /// <param name="addressbookId">Address book to add phone to</param>
        /// <param name="phone">Phone number to add</param>
        /// <param name="variables">Personalization variables list</param>
        /// <returns></returns>
        public int CreatePhoneInAddressbook(int addressbookId, string phone, params string[] variables)
        {
            return
                Run("addPhoneToAddressBook")
                    .WithParameter("idAddressBook", addressbookId)
                    .WithParameter("phone", phone)
                    .WithParameterIfNotNullOrWhiteSpace("variables", Phone.GetVariables(variables))
                    .Execute()
                    .To<ResponsePureImpl<PhoneCreateImpl>>(CheckForError)
                    .result.Id;
        }

        /// <summary>
        /// Add many phones to address book
        /// </summary>
        /// <param name="addressbookId">Address book to add phone to</param>
        /// <param name="phone">Phone object to add</param>
        /// <param name="phones">Additional phone object to add</param>
        /// <returns></returns>
        public bool CreatePhonesInAddressbook(int addressbookId, Phone phone, params Phone[] phones)
        {
            return Run("addPhoneToAddressBook")
                .WithParameter("idAddressBook", addressbookId)
                .WithParameter("data", AtomParkUtils.PhonesToJson(phone, phones))
                .Execute()
                .To<SuccessResultImpl>(CheckForError)
                .result.successful;
        }

        /// <summary>
        /// Fetch full information about one phone
        /// </summary>
        /// <param name="id">Id for the phone to get</param>
        /// <returns>Full information about phone</returns>
        public IPhone GetPhone(int id)
        {
            return
                Run("getPhoneFromAddressBook")
                    .WithParameter("idPhone", id)
                    .Execute()
                    .To<ResponseImpl<IPhone, PhoneImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// List all phones from all address books
        /// </summary>
        /// <param name="offset">Array offset to get elements</param>
        /// <param name="count">Count of elements to fetch from server</param>
        /// <returns>List of phones object</returns>
        public IPhones ListPhones(uint offset = 0, uint count = 50)
        {
            return ListPhonesFromBook(0, offset, count);
        }

        /// <summary>
        /// List all phones from specified address book
        /// </summary>
        /// <param name="addressbookId">Id of the address book to filter</param>
        /// <param name="offset">Array offset to get elements</param>
        /// <param name="count">Count of elements to fetch from server</param>
        /// <returns>List of phones object</returns>
        public IPhones ListPhonesFromBook(int addressbookId, uint offset = 0, uint count = 50)
        {
            return ListPhonesByPattern(null, addressbookId, offset, count);
        }

        /// <summary>
        /// List all phones from all address books
        /// </summary>
        /// <param name="phonePattern">Phone number to filter</param>
        /// <param name="addressbookId">Id of the address book to filter</param>
        /// <param name="offset">Array offset to get elements</param>
        /// <param name="count">Count of elements to fetch from server</param>
        /// <returns>List of phones object</returns>
        public IPhones ListPhonesByPattern(string phonePattern, int addressbookId = 0, uint offset = 0, uint count = 50)
        {
            return Run("getPhoneFromAddressBook")
                .WithParameter("from", offset)
                .WithParameter("offset", count)
                .WithParameterIfNotNullOrWhiteSpace("phone", phonePattern)
                .WithParameterIfGreaterThanZero("idAddressBook", addressbookId)
                .Execute()
                .To<ResponseImpl<IPhones, PhonesImpl>>(RemakeJsonForList)
                .result;
        }

        /// <summary>
        /// Edit phone information
        /// </summary>
        /// <param name="id">Id for the phone record to edit</param>
        /// <param name="newPhone">New phone number</param>
        /// <param name="newVariables">Optional. New variables list. If not specified, old is remained</param>
        /// <returns></returns>
        public bool EditPhone(int id, string newPhone, params string[] newVariables)
        {
            return Run("editPhone")
                .WithParameter("idPhone", id)
                .WithParameter("phone", newPhone)
                .WithParameterIfNotNullOrWhiteSpace("variables", Phone.GetVariables(newVariables)).Execute()
                .To<SuccessResultImpl>(CheckForError)
                .result.successful;
        }

        /// <summary>
        /// Delete phone number by id
        /// </summary>
        /// <param name="id">Id for the phone number to delete</param>
        /// <returns></returns>
        public bool DeletePhone(int id)
        {
            return
                Run("delPhoneFromAddressBook")
                    .WithParameter("idPhone", id)
                    .Execute()
                    .To<SuccessResultImpl>(CheckForError)
                    .result.successful;
        }
        
        /// <summary>
        /// Delete all phones from address book
        /// </summary>
        /// <param name="addressbookId">Address book id</param>
        /// <returns></returns>
        public bool DeleteAllPhonesFromAddressbook(int addressbookId)
        {
            return
                Run("delPhoneFromAddressBook")
                    .WithParameter("idAddressBook", addressbookId)
                    .Execute()
                    .To<SuccessResultImpl>(CheckForError)
                    .result.successful;
        }

        /// <summary>
        /// Delete phones by ids
        /// </summary>
        /// <param name="ids">List of ids to delete</param>
        /// <returns></returns>
        public bool DeletePhones(params int[] ids)
        {
            if (ids == null || ids.Length == 0)
                return false;
            return
                Run("delphonefromaddressbookgroup")
                    .WithParameter("idPhones", String.Join(",", ids))
                    .Execute()
                    .To<SuccessResultImpl>(CheckForError)
                    .result.successful;
        }

        /// <summary>
        /// Simple balance getter with default currency
        /// </summary>
        public IBalance Balance
        {
            get { return GetBalance(); }
        }

        /// <summary>
        /// Get current balance
        /// </summary>
        /// <param name="currency">Currency in which to get balance</param>
        /// <returns>Balance information</returns>
        public IBalance GetBalance(Currency? currency = null)
        {
            return
                Run("getUserBalance")
                    .WithContitionParameter(currency != null, "currency",
                        (currency == null) ? null : currency.Value.ToString())
                    .Execute()
                    .To<ResponseImpl<IBalance, BalanceImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// Make a request to register sender name
        /// </summary>
        /// <param name="senderName">New sender name. For digits should be 14 chars maximum. For letters - limit is 11 characters</param>
        /// <param name="country">Country of origin for the name</param>
        /// <returns>Id for the sender name</returns>
        public int RequestSenderRegistration(string senderName, Country country)
        {
            return Run("registerSender")
                .WithParameter("name", senderName)
                .WithParameter("country", country.AsString())
                .Execute()
                .To<ResponsePureImpl<SenderRegisterImpl>>(CheckForError)
                .result.Id;
        }

        /// <summary>
        /// Get full information about sender name by id
        /// </summary>
        /// <param name="id">Id of sender name to retrieve</param>
        /// <returns>Sender name object with full information</returns>
        public ISender GetSender(int id)
        {
            return
                Run("getSenderStatus")
                    .WithParameter("idName", id)
                    .Execute()
                    .To<ResponseImpl<ISender, SenderImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// Get full information about sender name by name and country
        /// </summary>
        /// <param name="name">Name to search</param>
        /// <param name="country">Country to filter</param>
        /// <returns>Sender name object with full information</returns>
        public ISender GetSender(string name, Country country)
        {
            return
                Run("getSenderStatus")
                    .WithParameter("name", name)
                    .WithParameter("country", country.AsString())
                    .Execute()
                    .To<ResponseImpl<ISender, SenderImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// Retrieve list of sender names for current account
        /// </summary>
        /// <param name="offset">Array offset to get elements</param>
        /// <param name="count">Count of elements to fetch from server</param>
        /// <returns>List of sender name objects with full information</returns>
        public ISenders ListSenders(uint offset = 0, uint count = 50)
        {
            return Run("getSenderStatus")
                .WithParameter("from", offset)
                .WithParameter("offset", count)
                .Execute()
                .To<ResponseImpl<ISenders, SendersImpl>>(RemakeJsonForList)
                .result;
        }

        /// <summary>
        /// Create a campaign only with one sms to send
        /// </summary>
        /// <param name="phoneNumber">Number for sms receiver</param>
        /// <param name="message">Message information object</param>
        /// <returns>New campaign information with id and price</returns>
        public ISendResult SendSms(string phoneNumber, MessageInfo message)
        {
            return
                message.FillCommand(Run("sendSMS").WithParameter("phone", phoneNumber))
                    .Execute()
                    .To<ResponseImpl<ISendResult, SendResultImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// Send sms to the list of phones from address book
        /// </summary>
        /// <param name="addressbookId">Id for the address book to send to</param>
        /// <param name="message">Message information object</param>
        /// <param name="watchdogPhoneNumber">Phone number to control the campaign send-out</param>
        /// <param name="portionSize">The count of sms to send at a time. Use this parameter if you would like to send sms by portions</param>
        /// <param name="portionInterval">Interval between portions in seconds</param>
        /// <returns>New campaign information with id and price</returns>
        public ISendResult SendSmsBatch(int addressbookId, MessageInfo message, string watchdogPhoneNumber = null,
            uint portionSize = 0, uint portionInterval = 0)
        {
            return
                message.FillCommand(Run("createCampaign")
                    .WithParameter("list_id", addressbookId)
                    .WithParameter("batch", portionSize)
                    .WithParameter("batchinterval", portionInterval)
                    .WithParameterIfNotNullOrWhiteSpace("control_phone", watchdogPhoneNumber))
                    .Execute()
                    .To<ResponseImpl<ISendResult, SendResultImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// Calculate price for sending sms to the list of phones from address book
        /// </summary>
        /// <param name="addressbookId">Id for the address book to send to</param>
        /// <param name="message">Message information object</param>
        /// <returns>Price information</returns>
        public ISendResult SendSmsBatchEstimate(int addressbookId, MessageInfo message)
        {
            return
                message.FillCommand(Run("checkCampaignPrice").WithParameter("list_id", addressbookId))
                    .Execute()
                    .To<ResponseImpl<ISendResult, SendResultImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// Send sms to the custom list of phones
        /// </summary>
        /// <param name="message">Message information object</param>
        /// <param name="phone">First custom phone</param>
        /// <param name="phones">Optional custom phones</param>
        /// <returns>New campaign information with id and price</returns>
        public ISendResult SendSmsBatch(MessageInfo message, Phone phone, params Phone[] phones)
        {
            return
                message.FillCommand(Run("sendsmsgroup").WithParameter("phones", AtomParkUtils.PhonesToJson(phone, phones)))
                    .Execute()
                    .To<ResponseImpl<ISendResult, SendResultImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// Calculate price for sending sms to the custom list of phones
        /// </summary>
        /// <param name="message">Message information object</param>
        /// <param name="phone">First custom phone</param>
        /// <param name="phones">Optional custom phones</param>
        /// <returns>Price information</returns>
        public ISendPrice SendSmsBatchEstimate(MessageInfo message, Phone phone, params Phone[] phones)
        {
            return
                message.FillCommand(Run("checkCampaignPriceGroup").WithParameter("phones", AtomParkUtils.PhonesToJson(phone, phones)))
                    .Execute()
                    .To<ResponseImpl<ISendPrice, SendPriceImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// Get short statistics on campaign
        /// </summary>
        /// <param name="id">Campaign id</param>
        /// <returns>Short statistics info</returns>
        public ICampaignInfo GetCampaign(int id)
        {
            return
                Run("getCampaignInfo").WithParameter("id", id)
                    .Execute()
                    .To<ResponseImpl<ICampaignInfo, CampaignInfoImpl>>(CheckForError)
                    .result;
        }

        /// <summary>
        /// Cancel campaign processing
        /// </summary>
        /// <param name="id">Campaign id</param>
        /// <returns>Success of the operation</returns>
        public bool CancelCampaign(int id)
        {
            return
                Run("cancelCampaign")
                    .WithParameter("id", id)
                    .Execute()
                    .To<SuccessResultImpl>(CheckForError)
                    .result.successful;
        }

        /// <summary>
        /// Delete campaign processing
        /// </summary>
        /// <param name="id">Campaign id</param>
        /// <returns>Success of the operation</returns>
        public bool DeleteCampaign(int id)
        {
            return
                Run("deleteCampaign")
                    .WithParameter("id", id)
                    .Execute()
                    .To<SuccessResultImpl>(CheckForError)
                    .result.successful;
        }

        /// <summary>
        /// Retrieve campaign statistics by each phone
        /// </summary>
        /// <param name="id">Campaign id</param>
        /// <param name="sinceDateTime">Filter only records from date</param>
        /// <returns>List of delivery information</returns>
        public List<ISmsDeliveryInfo> GetCampaignStatistics(int id, DateTime? sinceDateTime = null)
        {
            var cmd = Run("getCampaignDeliveryStats").WithParameter("id", id);
            if (sinceDateTime != null)
                cmd = cmd.WithParameter("datefrom", sinceDateTime.ToPochtaString());
            return cmd.Execute()
                .To<ResponsePureImpl<List<SmsDeliveryInfoImpl>>>(ConvertAtomParkArrayedResult)
                .result.ConvertAll(x => (ISmsDeliveryInfo) x);
        }

        /// <summary>
        /// List all campaigns in the account
        /// </summary>
        /// <returns>List of campaigns with short info</returns>
        public List<ICampaign> ListCampaigns()
        {
            return Run("getCampaignList").Execute()
                .To<ResponsePureImpl<List<CampaignImpl>>>(ConvertAtomParkArrayedResult)
                .result.ConvertAll(x => (ICampaign)x);
        }

        private static JsonValue ConvertAtomParkArrayedResult(JsonValue input)
        {
            return input.ConvertArrayedResult(CheckForError);
        }

        #region Not working
        private JsonValue ListCampaignsMessageStatuses(params int[] ids)
        {
            return Run("getcampaigndeliverystatsgroup")
                .WithParameter("id", String.Join(",", ids))
                .Execute().ToJson();
        }

        private JsonValue ListCampaignsDetailed(params int[] ids)
        {
            return Run("gettaskinfo")
                .WithParameter("taskIds", String.Join(",", ids))
                .Execute().ToJson();
        }
        #endregion

        /// <summary>
        /// Create new exception phone, which will not get sms messages
        /// </summary>
        /// <param name="phone">Phone number</param>
        /// <param name="comment">Optional comment/reason</param>
        /// <returns>Id for the newly generated exception</returns>
        public int CreateException(string phone, string comment = null)
        {
            return
                Run("addPhoneToExceptions")
                    .WithParameter("phone", phone)
                    .WithParameterIfNotNullOrWhiteSpace("reason", comment)
                    .Execute()
                    .To<ResponsePureImpl<PhoneExceptionCreateImpl>>(CheckForError)
                    .result.Id;
        }

        /// <summary>
        /// Create new exception phone, which will not get sms messages
        /// </summary>
        /// <param name="phoneId">Phone id in ePochta</param>
        /// <param name="comment">Optional comment/reason</param>
        /// <returns>Id for the newly generated exception</returns>
        public int CreateException(int phoneId, string comment = null)
        {
            return
                Run("addPhoneToExceptions")
                    .WithParameter("idPhone", phoneId)
                    .WithParameterIfNotNullOrWhiteSpace("reason", comment)
                    .Execute()
                    .To<ResponsePureImpl<PhoneExceptionCreateImpl>>(CheckForError)
                    .result.Id;
        }

        /// <summary>
        /// Edit exception comment
        /// </summary>
        /// <param name="id">Id of the exception to edit</param>
        /// <param name="comment">New comment/reason</param>
        /// <returns>Success of the operation</returns>
        public bool EditException(int id, string comment)
        {
            if (String.IsNullOrWhiteSpace(comment))
                return false;
            return
                Run("editExceptions")
                    .WithParameter("idException", id)
                    .WithParameter("reason", comment)
                    .Execute()
                    .To<SuccessResultImpl>(CheckForError)
                    .result.successful;
        }

        /// <summary>
        /// Get full infor about exception by id
        /// </summary>
        /// <param name="id">Id of the exception to get info</param>
        /// <returns>Full exception info object</returns>
        public IPhoneException GetException(int id)
        {
            return
                Run("getException")
                    .WithParameter("idException", id)
                    .Execute()
                    .To<ResponseImpl<IPhoneException, PhoneExceptionImpl>>(RemakeJsonForList)
                    .result;
        }

        /// <summary>
        /// List all exceptions
        /// </summary>
        /// <param name="offset">Array offset to get elements</param>
        /// <param name="count">Count of elements to fetch from server</param>
        /// <returns>List of exception objects with full information</returns>
        public IPhoneExceptions ListExceptions(uint offset = 0, uint count = 50)
        {
            return ListExceptionsFromBook(0, offset, count);
        }

        /// <summary>
        /// List all exceptions from address book
        /// </summary>
        /// <param name="addressbookId">Address book to filter the list</param>
        /// <param name="offset">Array offset to get elements</param>
        /// <param name="count">Count of elements to fetch from server</param>
        /// <returns>List of exception objects with full information</returns>
        public IPhoneExceptions ListExceptionsFromBook(int addressbookId, uint offset = 0, uint count = 50)
        {
            return ListExceptionsByPattern(null, addressbookId, offset, count);
        }

        /// <summary>
        /// List all exceptions filtered by phone
        /// </summary>
        /// <param name="phonePattern">Phone number to filter the list of exceptions</param>
        /// <param name="addressbookId">Address book to filter the list</param>
        /// <param name="offset">Array offset to get elements</param>
        /// <param name="count">Count of elements to fetch from server</param>
        /// <returns>List of exception objects with full information</returns>
        public IPhoneExceptions ListExceptionsByPattern(string phonePattern, int addressbookId = 0, uint offset = 0, uint count = 50)
        {
            return Run("getException")
                .WithParameter("from", offset)
                .WithParameter("offset", count)
                .WithParameterIfNotNullOrWhiteSpace("phone", phonePattern)
                .WithParameterIfGreaterThanZero("idAddressbook", addressbookId)
                .Execute()
                .To<ResponseImpl<IPhoneExceptions, PhoneExceptionsImpl>>(RemakeJsonForList)
                .result;
        }
    }
}
