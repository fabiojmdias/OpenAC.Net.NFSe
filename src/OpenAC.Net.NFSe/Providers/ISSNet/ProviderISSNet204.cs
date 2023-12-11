// ***********************************************************************
// Assembly         : OpenAC.Net.NFSe
// Author           : Rafael Dias
// Created          : 05-22-2018
//
// Last Modified By : Leandro Rossi (rossism.com.br)
// Last Modified On : 14-04-2023
// ***********************************************************************
// <copyright file="ProviderISSNet.cs" company="OpenAC .Net">
//		        		   The MIT License (MIT)
//	     		    Copyright (c) 2014 - 2023 Projeto OpenAC .Net
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

using OpenAC.Net.Core.Extensions;
using OpenAC.Net.DFe.Core.Extensions;
using OpenAC.Net.NFSe.Configuracao;
using System.Text;
using OpenAC.Net.NFSe.Nota;
using OpenAC.Net.DFe.Core;
using System.Linq;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core.Common;
using System;
using OpenAC.Net.DFe.Core.Serializer;
using OpenAC.Net.DFe.Core.Document;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Security.Policy;

namespace OpenAC.Net.NFSe.Providers;

internal sealed class ProviderISSNet204 : ProviderABRASF204
{
    #region Constructors

    public ProviderISSNet204(ConfigNFSe config, OpenMunicipioNFSe municipio) : base(config, municipio)
    {
        Name = "ISSNet";
    }

    #endregion Constructors

    #region Methods

    protected override IServiceClient GetClient(TipoUrl tipo) => new ISSNet204ServiceClient(this, tipo, Certificado);

    protected override string GetSchema(TipoUrl tipo) => "nfse.xsd";

    protected override string GerarCabecalho() => $"<cabecalho versao=\"2.04\" {GetNamespace()}><versaoDados>{Versao}</versaoDados></cabecalho>";

    #endregion Methods

    #region Services 

    protected override void AssinarConsultarNFSeRps(RetornoConsultarNFSeRps retornoWebservice)
    {
        retornoWebservice.XmlEnvio = XmlSigning.AssinarXmlTodos(retornoWebservice.XmlEnvio, "ConsultarNfseRpsEnvio", "", Certificado);
    }

    protected override void PrepararConsultarNFSeRps(RetornoConsultarNFSeRps retornoWebservice, NotaServicoCollection notas)
    {
        if (retornoWebservice.NumeroRps < 1)
        {
            retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "Número da RPS não informado para a consulta." });
            return;
        }

        var loteBuilder = new StringBuilder();
        loteBuilder.Append($"<ConsultarNfseRpsEnvio {GetNamespace()}>");
        loteBuilder.Append("<Pedido>");
        loteBuilder.Append("<IdentificacaoRps>");
        loteBuilder.Append($"<Numero>{retornoWebservice.NumeroRps}</Numero>");
        loteBuilder.Append($"<Serie>{retornoWebservice.Serie}</Serie>");
        loteBuilder.Append($"<Tipo>{(int)retornoWebservice.Tipo + 1}</Tipo>");
        loteBuilder.Append("</IdentificacaoRps>");
        loteBuilder.Append("<Prestador>");
        loteBuilder.Append("<CpfCnpj>");
        loteBuilder.Append(Configuracoes.PrestadorPadrao.CpfCnpj.IsCNPJ()
            ? $"<Cnpj>{Configuracoes.PrestadorPadrao.CpfCnpj.ZeroFill(14)}</Cnpj>"
            : $"<Cpf>{Configuracoes.PrestadorPadrao.CpfCnpj.ZeroFill(11)}</Cpf>");
        loteBuilder.Append("</CpfCnpj>");
        if (!Configuracoes.PrestadorPadrao.InscricaoMunicipal.IsEmpty()) loteBuilder.Append($"<InscricaoMunicipal>{Configuracoes.PrestadorPadrao.InscricaoMunicipal}</InscricaoMunicipal>");
        loteBuilder.Append("</Prestador>");
        loteBuilder.Append("</Pedido>");
        loteBuilder.Append("</ConsultarNfseRpsEnvio>");
        retornoWebservice.XmlEnvio = loteBuilder.ToString();
    }

    protected override void TratarRetornoSubstituirNFSe(RetornoSubstituirNFSe retornoWebservice, NotaServicoCollection notas)
    {
        // Analisa mensagem de retorno
        var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
        MensagemErro(retornoWebservice, xmlRet, "SubstituirNfseResult");
        if (retornoWebservice.Erros.Any()) return;

        var retornoLote = xmlRet.Root.ElementAnyNs("RetSubstituicao");
        var nfseSubstituida = retornoLote?.ElementAnyNs("NfseSubstituida");
        var nfseSubstituidora = retornoLote?.ElementAnyNs("NfseSubstituidora");

        if (nfseSubstituida == null) retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "NFSe Substituida não encontrada! (NfseSubstituida)" });
        if (nfseSubstituidora == null) retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "NFSe Substituidora não encontrada! (NfseSubstituidora)" });
        if (retornoWebservice.Erros.Any()) return;


        /******* TRATANDO A NOTA SUBSTITUÍDA *******/
        var compNfse = nfseSubstituida.ElementAnyNs("CompNfse");
        if (compNfse == null) retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "NFSe não encontrada! (CompNfse)" });
        if (retornoWebservice.Erros.Any()) return;

        var notaSubistituida = LoadXml(compNfse.ToString());

        var notaSubistituidaExistente = notas.FirstOrDefault(t => t.IdentificacaoRps.Numero == notaSubistituida.IdentificacaoRps.Numero);
        if (notaSubistituidaExistente == null)
        {
            notaSubistituidaExistente = notaSubistituida;
            notas.Add(notaSubistituidaExistente);
        }
        else
        {
            notaSubistituidaExistente.IdentificacaoNFSe.Numero = notaSubistituida.IdentificacaoNFSe.Numero;
            notaSubistituidaExistente.IdentificacaoNFSe.Chave = notaSubistituida.IdentificacaoNFSe.Chave;
            notaSubistituidaExistente.IdentificacaoNFSe.DataEmissao = notaSubistituida.IdentificacaoNFSe.DataEmissao;
            notaSubistituidaExistente.XmlOriginal = compNfse.ToString();
        }

        /******* TRATANDO A NOTA SUBSTITUIDORA *******/
        compNfse = nfseSubstituidora.ElementAnyNs("CompNfse");
        if (compNfse == null) retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "NFSe não encontrada! (CompNfse)" });
        if (retornoWebservice.Erros.Any()) return;

        var notaSubistituidora = LoadXml(compNfse.ToString());

        var notaSubistituidoraExistente = notas.FirstOrDefault(t => t.IdentificacaoRps.Numero == notaSubistituidora.IdentificacaoRps.Numero);
        if (notaSubistituidoraExistente == null)
        {
            notaSubistituidoraExistente = notaSubistituidora;
            notas.Add(notaSubistituidoraExistente);
        }
        else
        {
            notaSubistituidoraExistente.IdentificacaoNFSe.Numero = notaSubistituidora.IdentificacaoNFSe.Numero;
            notaSubistituidoraExistente.IdentificacaoNFSe.Chave = notaSubistituidora.IdentificacaoNFSe.Chave;
            notaSubistituidoraExistente.IdentificacaoNFSe.DataEmissao = notaSubistituidora.IdentificacaoNFSe.DataEmissao;
            notaSubistituidoraExistente.XmlOriginal = compNfse.ToString();
        }

        /******* TRATAMENTOS FINAIS *******/
        retornoWebservice.Sucesso = true;

        notaSubistituidoraExistente.RpsSubstituido.NFSeSubstituidora = notaSubistituidoraExistente.IdentificacaoNFSe.Numero;
        notaSubistituidoraExistente.RpsSubstituido.NumeroNfse = notaSubistituidaExistente.IdentificacaoNFSe.Numero;
        notaSubistituidoraExistente.RpsSubstituido.DataEmissaoNfseSubstituida = notaSubistituidaExistente.IdentificacaoNFSe.DataEmissao;
        notaSubistituidoraExistente.RpsSubstituido.Id = notaSubistituidaExistente.Id;
        notaSubistituidoraExistente.RpsSubstituido.NumeroRps = notaSubistituidaExistente.IdentificacaoRps.Numero;
        notaSubistituidoraExistente.RpsSubstituido.Serie = notaSubistituidaExistente.IdentificacaoRps.Serie;
        notaSubistituidoraExistente.RpsSubstituido.Signature = notaSubistituidaExistente.Signature;

        retornoWebservice.Nota = notaSubistituidoraExistente;
    }

    protected override void PrepararConsultarNFSe(RetornoConsultarNFSe retornoWebservice)
    {
        var loteBuilder = new StringBuilder();
        loteBuilder.Append($"<ConsultarNfseServicoPrestadoEnvio {GetNamespace()}>");
        loteBuilder.Append("<Pedido>");
        loteBuilder.Append("<Prestador>");
        loteBuilder.Append("<CpfCnpj>");
        loteBuilder.Append(Configuracoes.PrestadorPadrao.CpfCnpj.IsCNPJ()
            ? $"<Cnpj>{Configuracoes.PrestadorPadrao.CpfCnpj.ZeroFill(14)}</Cnpj>"
            : $"<Cpf>{Configuracoes.PrestadorPadrao.CpfCnpj.ZeroFill(11)}</Cpf>");
        loteBuilder.Append("</CpfCnpj>");
        if (!Configuracoes.PrestadorPadrao.InscricaoMunicipal.IsEmpty()) loteBuilder.Append($"<InscricaoMunicipal>{Configuracoes.PrestadorPadrao.InscricaoMunicipal}</InscricaoMunicipal>");
        loteBuilder.Append("</Prestador>");

        if (retornoWebservice.NumeroNFse > 0)
            loteBuilder.Append($"<NumeroNfse>{retornoWebservice.NumeroNFse}</NumeroNfse>");

        if (retornoWebservice.Inicio.HasValue && retornoWebservice.Fim.HasValue)
        {
            loteBuilder.Append("<PeriodoEmissao>");
            loteBuilder.Append($"<DataInicial>{retornoWebservice.Inicio:yyyy-MM-dd}</DataInicial>");
            loteBuilder.Append($"<DataFinal>{retornoWebservice.Fim:yyyy-MM-dd}</DataFinal>");
            loteBuilder.Append("</PeriodoEmissao>");
        }

        if (!retornoWebservice.CPFCNPJTomador.IsEmpty() && !retornoWebservice.IMTomador.IsEmpty())
        {
            loteBuilder.Append("<Tomador>");
            loteBuilder.Append("<CpfCnpj>");
            loteBuilder.Append(retornoWebservice.CPFCNPJTomador.IsCNPJ()
                ? $"<Cnpj>{retornoWebservice.CPFCNPJTomador.ZeroFill(14)}</Cnpj>"
                : $"<Cpf>{retornoWebservice.CPFCNPJTomador.ZeroFill(11)}</Cpf>");
            loteBuilder.Append("</CpfCnpj>");
            loteBuilder.Append($"<InscricaoMunicipal>{retornoWebservice.IMTomador}</InscricaoMunicipal>");
            loteBuilder.Append("</Tomador>");
        }

        if (!retornoWebservice.CPFCNPJIntermediario.IsEmpty())
        {
            loteBuilder.Append("<Intermediario>");
            loteBuilder.Append("<CpfCnpj>");
            loteBuilder.Append(retornoWebservice.CPFCNPJIntermediario.IsCNPJ()
                ? $"<Cnpj>{retornoWebservice.CPFCNPJIntermediario.ZeroFill(14)}</Cnpj>"
                : $"<Cpf>{retornoWebservice.CPFCNPJIntermediario.ZeroFill(11)}</Cpf>");
            loteBuilder.Append("</CpfCnpj>");
            if (!retornoWebservice.IMIntermediario.IsEmpty())
                loteBuilder.Append($"<InscricaoMunicipal>{retornoWebservice.IMIntermediario}</InscricaoMunicipal>");
            loteBuilder.Append("</Intermediario>");
        }

        loteBuilder.Append($"<Pagina>{Math.Max(retornoWebservice.Pagina, 1)}</Pagina>");
        loteBuilder.Append("</Pedido>");
        loteBuilder.Append("</ConsultarNfseServicoPrestadoEnvio>");
        retornoWebservice.XmlEnvio = loteBuilder.ToString();
    }

    protected override void AssinarConsultarNFSe(RetornoConsultarNFSe retornoWebservice)
    {
        retornoWebservice.XmlEnvio = XmlSigning.AssinarXmlTodos(retornoWebservice.XmlEnvio, "ConsultarNfseServicoPrestadoEnvio", "", Certificado);
    }

    protected override XElement WriteTomadorRps(NotaServico nota)
    {
        if (nota.Tomador.CpfCnpj.IsEmpty()) return null;

        var tomador = new XElement("TomadorServico");

        var idTomador = new XElement("IdentificacaoTomador");
        tomador.Add(idTomador);

        var cpfCnpjTomador = new XElement("CpfCnpj");
        idTomador.Add(cpfCnpjTomador);

        cpfCnpjTomador.AddChild(AdicionarTagCNPJCPF("", "Cpf", "Cnpj", nota.Tomador.CpfCnpj));

        idTomador.AddChild(AdicionarTag(TipoCampo.Str, "", "InscricaoMunicipal", 1, 150, Ocorrencia.NaoObrigatoria, nota.Tomador.InscricaoMunicipal));

        tomador.AddChild(AdicionarTag(TipoCampo.Str, "", "NifTomador", 1, 150, Ocorrencia.NaoObrigatoria, nota.Tomador.DocTomadorEstrangeiro));
        tomador.AddChild(AdicionarTag(TipoCampo.Str, "", "RazaoSocial", 1, 150, Ocorrencia.Obrigatoria, nota.Tomador.RazaoSocial));

        if (nota.Tomador.EnderecoExterior.CodigoPais > 0)
        {
            var enderecoExt = new XElement("EnderecoExterior");
            tomador.Add(enderecoExt);

            enderecoExt.AddChild(AdicionarTag(TipoCampo.Int, "", "CodigoPais", 8, 8, Ocorrencia.Obrigatoria, nota.Tomador.EnderecoExterior.CodigoPais));
            enderecoExt.AddChild(AdicionarTag(TipoCampo.Str, "", "EnderecoCompletoExterior", 8, 8, Ocorrencia.Obrigatoria, nota.Tomador.EnderecoExterior.EnderecoCompleto));
        }
        else
        {
            var endereco = new XElement("Endereco");
            tomador.Add(endereco);

            endereco.AddChild(AdicionarTag(TipoCampo.Str, "", "Endereco", 1, 125, Ocorrencia.Obrigatoria, nota.Tomador.Endereco.Logradouro));
            endereco.AddChild(AdicionarTag(TipoCampo.Str, "", "Numero", 1, 10, Ocorrencia.Obrigatoria, nota.Tomador.Endereco.Numero));
            endereco.AddChild(AdicionarTag(TipoCampo.Str, "", "Complemento", 1, 60, Ocorrencia.NaoObrigatoria, nota.Tomador.Endereco.Complemento));
            endereco.AddChild(AdicionarTag(TipoCampo.Str, "", "Bairro", 1, 60, Ocorrencia.Obrigatoria, nota.Tomador.Endereco.Bairro));
            endereco.AddChild(AdicionarTag(TipoCampo.Int, "", "CodigoMunicipio", 7, 7, Ocorrencia.Obrigatoria, nota.Tomador.Endereco.CodigoMunicipio));
            endereco.AddChild(AdicionarTag(TipoCampo.Str, "", "Uf", 2, 2, Ocorrencia.Obrigatoria, nota.Tomador.Endereco.Uf));
            endereco.AddChild(AdicionarTag(TipoCampo.StrNumber, "", "Cep", 8, 8, Ocorrencia.Obrigatoria, nota.Tomador.Endereco.Cep));
        }

        if (nota.Tomador.DadosContato.Email.IsEmpty() && nota.Tomador.DadosContato.Telefone.IsEmpty()) return tomador;

        var contato = new XElement("Contato");
        tomador.Add(contato);

        contato.AddChild(AdicionarTag(TipoCampo.Str, "", "Telefone", 8, 8, Ocorrencia.NaoObrigatoria, nota.Tomador.DadosContato.Telefone));
        contato.AddChild(AdicionarTag(TipoCampo.Str, "", "Email", 8, 8, Ocorrencia.NaoObrigatoria, nota.Tomador.DadosContato.Email));

        return tomador;
    }

    private void TratarRetornoConsultarUrlVisualizacaoNfse(RetornoConsultarUrlVisualizacaoNfse retornoWebservice)
    {
        var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
        MensagemErro(retornoWebservice, xmlRet.Root, "ConsultarUrlNfseResposta");
        if (retornoWebservice.Erros.Any()) return;
        var retornoRoot = xmlRet.ElementAnyNs("ConsultarUrlNfseResposta");
        var url = retornoRoot.Descendants().Where(e => e.Name.LocalName == "UrlVisualizacaoNfse").FirstOrDefault()?.GetValue<string>() ?? string.Empty;
        retornoWebservice.Url = url;
        retornoWebservice.Sucesso = true;
    }

    private void PrepararConsultarUrlVisualizacaoNfse(RetornoConsultarUrlVisualizacaoNfse retornoWebservice)
    {
        var loteBuilder = new StringBuilder();
        loteBuilder.Append($"<ConsultarUrlNfseEnvio {GetNamespace()}>");
        loteBuilder.Append("<Pedido>");
        loteBuilder.Append("<Prestador>");
        loteBuilder.Append("<CpfCnpj>");
        loteBuilder.Append(Configuracoes.PrestadorPadrao.CpfCnpj.IsCNPJ()
            ? $"<Cnpj>{Configuracoes.PrestadorPadrao.CpfCnpj.ZeroFill(14)}</Cnpj>"
            : $"<Cpf>{Configuracoes.PrestadorPadrao.CpfCnpj.ZeroFill(11)}</Cpf>");
        loteBuilder.Append("</CpfCnpj>");
        if (!Configuracoes.PrestadorPadrao.InscricaoMunicipal.IsEmpty()) loteBuilder.Append($"<InscricaoMunicipal>{Configuracoes.PrestadorPadrao.InscricaoMunicipal}</InscricaoMunicipal>");
        loteBuilder.Append("</Prestador>");
        loteBuilder.Append($"<NumeroNfse>{retornoWebservice.NumeroNFSe}</NumeroNfse>");
        loteBuilder.Append($"<Pagina>1</Pagina>");   //Deixei fixo pag. 1 porque a consulta será feita somente pelo numero da nota
        loteBuilder.Append("</Pedido>");
        loteBuilder.Append("</ConsultarUrlNfseEnvio>");
        retornoWebservice.XmlEnvio = loteBuilder.ToString();
    }

    public override RetornoConsultarUrlVisualizacaoNfse ConsultarUrlVisualizacaoNfse(string numeroNFSe, string codigoTributacaoMunicipio)
    {
        var retornoWebservice = new RetornoConsultarUrlVisualizacaoNfse()
        {
            NumeroNFSe = numeroNFSe,
            CodigoTributacaoMunicipio = codigoTributacaoMunicipio,
        };

        try
        {
            PrepararConsultarUrlVisualizacaoNfse(retornoWebservice);
            if (retornoWebservice.Erros.Any()) return retornoWebservice;
            if (Configuracoes.Geral.RetirarAcentos)
                retornoWebservice.XmlEnvio = retornoWebservice.XmlEnvio.RemoveAccent();

            //Assinar
            retornoWebservice.XmlEnvio = XmlSigning.AssinarXmlTodos(retornoWebservice.XmlEnvio, "ConsultarUrlNfseEnvio", "", Certificado);
            GravarArquivoEmDisco(retornoWebservice.XmlEnvio, $"ConsultarUrlVisualizacaoNfse-{numeroNFSe}-env.xml");
            // Remover a declaração do Xml se tiver
            retornoWebservice.XmlEnvio = retornoWebservice.XmlEnvio.RemoverDeclaracaoXml();
            ValidarSchema(retornoWebservice, GetSchema(TipoUrl.ConsultarNFSe));  //Pode ser qualquer tipo
            if (retornoWebservice.Erros.Any()) return retornoWebservice;

            // Recebe mensagem de retorno
            using (var cliente = GetClient(TipoUrl.ConsultarNFSe))   //Não tem uma url especifica
            {
                retornoWebservice.XmlRetorno = cliente.ConsultarUrlVisualizacaoNfse(GerarCabecalho(), retornoWebservice.XmlEnvio);
                retornoWebservice.EnvelopeEnvio = cliente.EnvelopeEnvio;
                retornoWebservice.EnvelopeRetorno = cliente.EnvelopeRetorno;
            }

            GravarArquivoEmDisco(retornoWebservice.XmlRetorno, $"ConsultarUrlVisualizacaoNfse-{numeroNFSe}-ret.xml");
            TratarRetornoConsultarUrlVisualizacaoNfse(retornoWebservice);
            return retornoWebservice;
        }
        catch (Exception ex)
        {
            retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = ex.Message });
            return retornoWebservice;
        }
    }




    #endregion
}