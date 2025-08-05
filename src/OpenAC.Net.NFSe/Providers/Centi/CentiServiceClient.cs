// ***********************************************************************
// Assembly         : OpenAC.Net.NFSe
// Author           : Fabio Dias (ZCoder Sistemas)
// Created          : 08-01-2025
//
// ***********************************************************************
// <copyright file="ProviderBase.cs" company="OpenAC .Net">
//		        		   The MIT License (MIT)
//	     		Copyright (c) 2014 - 2025 Projeto OpenAC .Net
//
//	 Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//	 The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//	 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// </copyright>
// <summary></summary>
// ***********************************************************************


using OpenAC.Net.NFSe.Commom.Client;
using OpenAC.Net.NFSe.Commom.Interface;
using OpenAC.Net.NFSe.Commom.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAC.Net.NFSe.Providers.Centi;

internal sealed class CentiServiceClient : NFSeRestServiceClient, IServiceClient
{
    #region Constructors
    public CentiServiceClient(ProviderBase provider, TipoUrl tipoUrl) : base(provider, tipoUrl)
    {
    }
    #endregion

    #region Methods

    public string CancelarNFSe(string? cabec, string msg)
    {
        throw new NotImplementedException();
    }

    public string CancelarNFSeLote(string? cabec, string msg)
    {
        throw new NotImplementedException();
    }

    public string ConsultarLoteRps(string? cabec, string msg)
    {
        throw new NotImplementedException();
    }

    public string ConsultarNFSe(string? cabec, string msg)
    {
        throw new NotImplementedException();
    }

    public string ConsultarNFSeRps(string? cabec, string msg)
    {
        string json = "{\"xml\": \"" + msg.Replace("\"", "\\\"") + "\", \"usuario\": \"" + Provider.Configuracoes.WebServices.Usuario +
            "\", \"senha\": \"" + Provider.Configuracoes.WebServices.Senha + "\" }";
        return Post("", json);
    }

    public string ConsultarSequencialRps(string? cabec, string msg)
    {
        throw new NotImplementedException();
    }

    public string ConsultarSituacao(string? cabec, string msg)
    {
        throw new NotImplementedException();
    }

    public string Enviar(string? cabec, string msg)
    {
        throw new NotImplementedException();
    }

    public string EnviarSincrono(string? cabec, string msg)
    {
        string json = "{\"xml\": \"" + msg.Replace("\"", "\\\"") + "\", \"usuario\": \"" + Provider.Configuracoes.WebServices.Usuario +
            "\", \"senha\": \"" + Provider.Configuracoes.WebServices.Senha + "\" }";
        return Post("", json);
    }

    public string SubstituirNFSe(string? cabec, string msg)
    {
        throw new NotImplementedException();
    }

    #endregion
}
