using OpenAC.Net.NFSe.Nota;
using OpenAC.Net.NFSe.Configuracao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenAC.Net.NFSe.Providers.Agili;
using OpenAC.Net.Core.Extensions;
using System.ServiceModel.Channels;
using System.Xml;
using OpenAC.Net.DFe.Core.Serializer;
using OpenAC.Net.Core;

namespace OpenAC.Net.NFSe.Providers
{
    internal sealed class ProviderAgili : ProviderBase
    {
        public ProviderAgili(ConfigNFSe config, OpenMunicipioNFSe municipio) : base(config, municipio)
        {
            Name = "Agili";
        }

        private string MontarXmlIdentificacaoPrestador()
        {
            var result = new StringBuilder();
            result.Append("<IdentificacaoPrestador>");
            result.Append($"<ChaveDigital>{Configuracoes.PrestadorPadrao.NumeroEmissorRps}</ChaveDigital>");
            result.Append("<CpfCnpj>");
            result.Append($"<Cnpj>{Configuracoes.PrestadorPadrao.CpfCnpj}</Cnpj>");
            result.Append("</CpfCnpj>");
            result.Append($"<InscricaoMunicipal>{Configuracoes.PrestadorPadrao.InscricaoMunicipal}</InscricaoMunicipal>");
            result.Append("</IdentificacaoPrestador>");
            return result.ToString();
        }

        private string MontarXmlIdentificacaoRps(NotaServico nota)
        {
            var result = new StringBuilder();
            result.Append("<Rps>");
            result.Append("<IdentificacaoRps>");
            result.Append($"<Numero>{nota.IdentificacaoRps.Numero}</Numero>");
            result.Append($"<Serie>{nota.IdentificacaoRps.Serie}</Serie>");
            int tipoRpsAgili = -2; //RPS
            switch (nota.IdentificacaoRps.Tipo)
            {
                case TipoRps.NFConjugada:
                    tipoRpsAgili = -4;
                    break;
                case TipoRps.Cupom:
                    tipoRpsAgili = -5;
                    break;
                default:
                    tipoRpsAgili = -2;
                    break;
            }
            result.Append($"<Tipo>{tipoRpsAgili}</Tipo>");
            result.Append("</IdentificacaoRps>");
            result.Append($"<DataEmissao>{nota.IdentificacaoRps.DataEmissao:yyyy-MM-dd}</DataEmissao>");
            result.Append($"</Rps>");
            return result.ToString();
        }

        private string MontarXmlDadosTomador(NotaServico nota)
        {
            var dadosTomador = new XElement("DadosTomador");
            if (!nota.Tomador.CpfCnpj.IsEmpty())
            {
                var ideTomador = new XElement("IdentificacaoTomador");
                dadosTomador.Add(ideTomador);
                var cpfCnpjTomador = new XElement("CpfCnpj");
                ideTomador.Add(cpfCnpjTomador);
                cpfCnpjTomador.AddChild(AdicionarTagCNPJCPF("", "Cpf", "Cnpj", nota.Tomador.CpfCnpj));
                ideTomador.AddChild(AdicionarTag(TipoCampo.Str, "", "InscricaoMunicipal", 1, 15, Ocorrencia.NaoObrigatoria, nota.Tomador.InscricaoMunicipal));
            }
            dadosTomador.AddChild(AdicionarTag(TipoCampo.Str, "", "RazaoSocial", 1, 115, Ocorrencia.Obrigatoria, nota.Tomador.RazaoSocial));
            dadosTomador.AddChild(AdicionarTag(TipoCampo.Int, "", "LocalEndereco", 1, 1, Ocorrencia.Obrigatoria,
                nota.Tomador.Endereco.CodigoPais == 1058 ? 1 : 2));
            if (nota.Tomador.Endereco.CodigoPais != 1058) { throw new Exception("Endereço do tomador no exterior não implementado!"); }

            var endereco = new XElement("Endereco");
            dadosTomador.Add(endereco);
            endereco.AddChild(AdicionarTag(TipoCampo.Str, "", "TipoLogradouro", 1, 120, Ocorrencia.Obrigatoria, string.IsNullOrEmpty(nota.Tomador.Endereco.TipoLogradouro) ?
                "RUA" : nota.Tomador.Endereco.TipoLogradouro));
            endereco.AddChild(AdicionarTag(TipoCampo.Str, "", "Logradouro", 1, 120, Ocorrencia.Obrigatoria, nota.Tomador.Endereco.Logradouro));
            endereco.AddChild(AdicionarTag(TipoCampo.Int, "", "Numero", 1, 10, Ocorrencia.NaoObrigatoria, nota.Tomador.Endereco.Numero.OnlyNumbers()));
            endereco.AddChild(AdicionarTag(TipoCampo.Str, "", "Complemento", 1, 300, Ocorrencia.NaoObrigatoria, nota.Tomador.Endereco.Complemento));
            endereco.AddChild(AdicionarTag(TipoCampo.Str, "", "Bairro", 1, 120, Ocorrencia.NaoObrigatoria, nota.Tomador.Endereco.Bairro));
            var municipio = new XElement("Municipio");
            endereco.Add(municipio);
            municipio.AddChild(AdicionarTag(TipoCampo.Int, "", "CodigoMunicipioIBGE", 1, 7, Ocorrencia.Obrigatoria, nota.Tomador.Endereco.CodigoMunicipio));
            municipio.AddChild(AdicionarTag(TipoCampo.Str, "", "Descricao", 1, 300, Ocorrencia.NaoObrigatoria, nota.Tomador.Endereco.Municipio));
            municipio.AddChild(AdicionarTag(TipoCampo.Str, "", "Uf", 1, 4, Ocorrencia.NaoObrigatoria, nota.Tomador.Endereco.Uf));
            var pais = new XElement("Pais");
            endereco.Add(pais);
            pais.AddChild(AdicionarTag(TipoCampo.Int, "", "CodigoPaisBacen", 1, 4, Ocorrencia.Obrigatoria, nota.Tomador.Endereco.CodigoPais));
            pais.AddChild(AdicionarTag(TipoCampo.Str, "", "Descricao", 1, 300, Ocorrencia.NaoObrigatoria, nota.Tomador.Endereco.Pais));
            endereco.AddChild(AdicionarTag(TipoCampo.Str, "", "Cep", 1, 8, Ocorrencia.Obrigatoria, nota.Tomador.Endereco.Cep));
            if (!string.IsNullOrWhiteSpace(nota.Tomador.DadosContato?.Telefone) || !string.IsNullOrWhiteSpace(nota.Tomador.DadosContato?.Email))
            {
                var contato = new XElement("Contato");
                dadosTomador.Add(contato);
                if (!string.IsNullOrWhiteSpace(nota.Tomador.DadosContato?.Telefone))
                {
                    contato.AddChild(AdicionarTag(TipoCampo.Str, "", "Telefone", 1, 14, Ocorrencia.NaoObrigatoria, nota.Tomador.DadosContato?.DDD + nota.Tomador.DadosContato?.Telefone));
                }
                if (!string.IsNullOrWhiteSpace(nota.Tomador.DadosContato?.Email))
                {
                    contato.AddChild(AdicionarTag(TipoCampo.Str, "", "Email", 1, 300, Ocorrencia.NaoObrigatoria, nota.Tomador.DadosContato.Email));
                }
            }
            dadosTomador.AddChild(AdicionarTag(TipoCampo.Str, "", "InscricaoEstadual", 1, 20, Ocorrencia.NaoObrigatoria, nota.Tomador.InscricaoEstadual));
            return dadosTomador.ToString(SaveOptions.DisableFormatting);
        }

        private string MontarXmlDadosIntermediario(NotaServico nota)
        {
            if (string.IsNullOrWhiteSpace(nota.Intermediario?.RazaoSocial)) { return ""; }
            // fabio 11/09/2023 07:10 - Método ainda não implementado
            return "";
            /*
    <DadosIntermediario>
      <IdentificacaoIntermediario>
        <CpfCnpj>
          <Cpf>Cpf_______1</Cpf>
        </CpfCnpj>
        <InscricaoMunicipal>InscricaoMunicipal1</InscricaoMunicipal>
      </IdentificacaoIntermediario>
      <RazaoSocial>RazaoSocial1</RazaoSocial>
    </DadosIntermediario>
             */
        }

        private string MontarXmlRegimeEspecialTributacao(NotaServico nota)
        {
            int codigoRegime = 0;
            string optanteSimples = "0";
            switch (nota.RegimeEspecialTributacao)
            {
                case RegimeEspecialTributacao.Estimativa:
                    codigoRegime = -2;
                    break;
                case RegimeEspecialTributacao.SociedadeProfissionais:
                    codigoRegime = -3;
                    break;
                case RegimeEspecialTributacao.Cooperativa:
                    codigoRegime = -4;
                    break;
                case RegimeEspecialTributacao.MicroEmpresarioIndividual:
                    codigoRegime = -5;
                    optanteSimples = "1";
                    break;
                case RegimeEspecialTributacao.MicroEmpresarioEmpresaPP:
                    codigoRegime = -6;
                    break;
                case RegimeEspecialTributacao.SimplesNacional:
                    codigoRegime = -6;
                    optanteSimples = "1";
                    break;
                default:
                    optanteSimples = "0";
                    break;
            }
            var result = new StringBuilder();
            if (codigoRegime < 0)
            {
                var regimeTributacao = new XElement("RegimeEspecialTributacao");
                regimeTributacao.AddChild(AdicionarTag(TipoCampo.Int, "", "Codigo", 1, 2, Ocorrencia.Obrigatoria, codigoRegime));
                //Descrição do regime não é obrigatoria
                result.Append(regimeTributacao.ToString(SaveOptions.DisableFormatting));
            }
            result.Append($"<OptanteSimplesNacional>{optanteSimples}</OptanteSimplesNacional>");
            result.Append($"<OptanteMEISimei>{(nota.RegimeEspecialTributacao == RegimeEspecialTributacao.MicroEmpresarioIndividual ? 1 : 0)}</OptanteMEISimei>");
            return result.ToString();
        }

        private string MontarXmlResponsavelRetencao(NotaServico nota)
        {
            var result = new StringBuilder();
            bool issRetido = nota.Servico.Valores.IssRetido == SituacaoTributaria.Retencao;
            result.Append($"<ISSQNRetido>{(issRetido ? 1 : 0)}</ISSQNRetido>");
            result.Append("<ResponsavelISSQN>");
            result.Append($"<Codigo>{(issRetido ? -1 : -3)}</Codigo>");
            result.Append("</ResponsavelISSQN>");
            return result.ToString();
        }

        private string MontarXmlMunicipioIncidencia(NotaServico nota)
        {
            var result = new StringBuilder();
            result.Append("<MunicipioIncidencia>");
            result.Append($"<CodigoMunicipioIBGE>{nota.Servico.MunicipioIncidencia}</CodigoMunicipioIBGE>");
            //Opcional
            //result.Append("<Descricao>TANGARA DA SERRA</Descricao>");
            //result.Append("<Uf>MT</Uf>");
            result.Append("</MunicipioIncidencia>");
            return result.ToString();
        }

        private string MontarXmlValoresRps(NotaServico nota)
        {
            var root = new XElement("root");
            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorServicos", 1, 15, Ocorrencia.Obrigatoria, nota.Servico.Valores.ValorServicos));
            decimal totalDesconto = nota.Servico.Valores.DescontoCondicionado + nota.Servico.Valores.DescontoIncondicionado;
            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorDescontos", 1, 15, Ocorrencia.MaiorQueZero, totalDesconto));
            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorPis", 1, 15, Ocorrencia.MaiorQueZero, nota.Servico.Valores.ValorPis));
            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorCofins", 1, 15, Ocorrencia.MaiorQueZero, nota.Servico.Valores.ValorCofins));
            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorInss", 1, 15, Ocorrencia.MaiorQueZero, nota.Servico.Valores.ValorInss));
            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorIrrf", 1, 15, Ocorrencia.MaiorQueZero, nota.Servico.Valores.ValorIr));
            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorCsll", 1, 15, Ocorrencia.MaiorQueZero, nota.Servico.Valores.ValorCsll));
            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorOutrasRetencoes", 1, 15, Ocorrencia.MaiorQueZero, nota.Servico.Valores.ValorOutrasRetencoes));
            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorBaseCalculoISSQN", 1, 15, Ocorrencia.MaiorQueZero, nota.Servico.Valores.BaseCalculo));
            root.Add(AdicionarTag(TipoCampo.De2, "", "AliquotaISSQN", 1, 7, Ocorrencia.MaiorQueZero, nota.Servico.Valores.Aliquota));
            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorISSQNCalculado", 1, 15, Ocorrencia.MaiorQueZero, nota.Servico.Valores.ValorIss));
            if (nota.Servico.Valores.IssRetido != SituacaoTributaria.Retencao)
                root.Add(AdicionarTag(TipoCampo.De2, "", "ValorISSQNRecolher", 1, 15, Ocorrencia.MaiorQueZero, nota.Servico.Valores.ValorIss));

            root.Add(AdicionarTag(TipoCampo.De2, "", "ValorLiquido", 1, 15, Ocorrencia.Obrigatoria, nota.Servico.Valores.ValorLiquidoNfse));
            root.Add(AdicionarTag(TipoCampo.Str, "", "Observacao", 1, 15, Ocorrencia.NaoObrigatoria, "Gerado via WebService"));
            string result = root.ToString(SaveOptions.DisableFormatting).Replace("<root>", "").Replace("</root>", "");
            return result;
        }

        private string MontarXmlListaServico(NotaServico nota)
        {
            var listaServico = new XElement("ListaServico");
            var dadosServico = new XElement("DadosServico");
            listaServico.Add(dadosServico);
            dadosServico.AddChild(AdicionarTag(TipoCampo.Str, "", "Discriminacao", 1, 2000, Ocorrencia.Obrigatoria, nota.Servico.Discriminacao));
            dadosServico.AddChild(AdicionarTag(TipoCampo.Str, "", "CodigoCnae", 1, 140, Ocorrencia.NaoObrigatoria, nota.Servico.CodigoCnae));
            dadosServico.AddChild(AdicionarTag(TipoCampo.De2, "", "Quantidade", 1, 18, Ocorrencia.Obrigatoria, 1)); //Fixo
            dadosServico.AddChild(AdicionarTag(TipoCampo.De2, "", "ValorServico", 1, 18, Ocorrencia.Obrigatoria, nota.Servico.Valores.ValorServicos));
            //dadosServico.AddChild(AdicionarTag(TipoCampo.De2, "", "ValorDesconto", 1, 8, Ocorrencia.Obrigatoria, nota.Servico.Valores.DescontoIncondicionado)); //Não precisa informar
            return listaServico.ToString(SaveOptions.DisableFormatting);
        }


        private string MontarXmlExigibilidadeIssqn(NotaServico nota)
        {
            int codigo = -1;
            string descricao = "";
            switch (nota.Servico.ExigibilidadeIss)
            {
                case ExigibilidadeIss.Exigivel:
                    codigo = -1;
                    descricao = "Exigível";
                    break;
                case ExigibilidadeIss.NaoIncidencia:
                    codigo = -2;
                    descricao = "Não incidência";
                    break;
                case ExigibilidadeIss.Isencao:
                    codigo = -3;
                    descricao = "Isento";
                    break;
                case ExigibilidadeIss.Exportacao:
                    codigo = -4;
                    descricao = "Exportação";
                    break;
                case ExigibilidadeIss.Imunidade:
                    codigo = -5;
                    descricao = "Imune";
                    break;
                case ExigibilidadeIss.SuspensaDecisaoJudicial:
                    codigo = -6;
                    descricao = "Exigibilidade suspensa por decisão judicial";
                    break;
                case ExigibilidadeIss.SuspensaProcessoAdministrativo:
                    codigo = -7;
                    descricao = "Exigibilidade suspensa por processo administrativo";
                    break;
                case ExigibilidadeIss.Fixo:
                    codigo = -8;
                    descricao = "Fixo";
                    break;

                case ExigibilidadeIss.IsentoPorLeiEspecifica:
                    codigo = -9;
                    descricao = "Isento por lei específica";
                    break;
            }
            var result = new StringBuilder();
            result.Append("<ExigibilidadeISSQN>");
            result.Append($"<Codigo>{codigo}</Codigo>");
            result.Append($"<Descricao>{descricao}</Descricao>");
            result.Append("</ExigibilidadeISSQN>");
            return result.ToString();
        }

        private void LoadNFSe(NotaServico nota, XElement rootNFSe)
        {
            nota.IdentificacaoNFSe.Numero = rootNFSe.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty;
            nota.IdentificacaoNFSe.Chave = rootNFSe.ElementAnyNs("CodigoAutenticidade")?.GetValue<string>() ?? string.Empty;
            nota.IdentificacaoNFSe.DataEmissao = rootNFSe.ElementAnyNs("DataEmissao")?.GetValue<DateTime>() ?? DateTime.MinValue;
            int codSituacao = rootNFSe.ElementAnyNs("SituacaoNfse")?.ElementAnyNs("Codigo")?.GetValue<int>() ?? -8;
            nota.Situacao = codSituacao == -2 ? SituacaoNFSeRps.Cancelado : SituacaoNFSeRps.Normal;
            var rootRps = rootNFSe.ElementAnyNs("DeclaracaoPrestacaoServico");
            var cpfcnpjTomador = rootRps.ElementAnyNs("DadosTomador")?.ElementAnyNs("IdentificacaoTomador")?.ElementAnyNs("CpfCnpj");
            nota.Tomador.CpfCnpj = cpfcnpjTomador.ElementAnyNs("Cnpj")?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nota.Tomador.CpfCnpj))
                nota.Tomador.CpfCnpj = cpfcnpjTomador.ElementAnyNs("Cpf")?.GetValue<string>() ?? string.Empty;
            nota.Servico.Valores.ValorLiquidoNfse = rootRps.ElementAnyNs("ValorLiquido")?.GetValue<decimal>() ?? 0;
            //Carregar demais campos caso necessário.
        }

        protected override void PrepararConsultarNFSeRps(RetornoConsultarNFSeRps retornoWebservice, NotaServicoCollection notas)
        {
            var message = new StringBuilder();
            message.Append("<ConsultarNfseRpsEnvio xmlns=\"http://www.agili.com.br/nfse_v_1.00.xsd\">");
            message.Append($"<UnidadeGestora>{Municipio.Cnpj}</UnidadeGestora>");
            message.Append("<IdentificacaoRps>");
            message.Append($"<Numero>{retornoWebservice.NumeroRps}</Numero>");
            message.Append($"<Serie>{retornoWebservice.Serie}</Serie>");
            message.Append("<Tipo>-2</Tipo>"); //Tipo Rps
            message.Append("</IdentificacaoRps>");
            message.Append(MontarXmlIdentificacaoPrestador());
            message.Append("<Versao>1.00</Versao>");
            message.Append($"</ConsultarNfseRpsEnvio>");
            retornoWebservice.XmlEnvio = message.ToString();
        }

        protected override void AssinarConsultarNFSeRps(RetornoConsultarNFSeRps retornoWebservice)
        {
            return;
        }

        protected override bool PrecisaValidarSchema(TipoUrl tipo)
        {
            return false;
            //return true;    //Talvez pode ser desativado
        }

        protected override string GetSchema(TipoUrl tipo)
        {
            return "WebAPI-NFSe.xsd";
        }

        protected override IServiceClient GetClient(TipoUrl tipo)
        {
            return new AgileServiceClient(this, tipo);
        }

        protected override string GerarCabecalho()
        {
            return string.Empty;
        }

        protected override void TratarRetornoConsultarNFSeRps(RetornoConsultarNFSeRps retornoWebservice, NotaServicoCollection notas)
        {
            var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
            MensagemErro(retornoWebservice, xmlRet, "ConsultarNfseRpsResposta");
            if (retornoWebservice.Erros.Any()) return;
            var nfse = xmlRet.ElementAnyNs("Nfse");
            if (nfse == null)
            {
                retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "NFSe não encontrada! (ListaNfse)" });
                return;
            }
            var numeroNFSe = nfse.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty;
            var chaveNFSe = nfse.ElementAnyNs("CodigoAutenticidade")?.GetValue<string>() ?? string.Empty;
            var dataNFSe = nfse.ElementAnyNs("DataEmissao")?.GetValue<DateTime>() ?? DateTime.Now;
            var numeroRps = nfse.ElementAnyNs("DeclaracaoPrestacaoServico")?
                                .ElementAnyNs("Rps")?
                                .ElementAnyNs("IdentificacaoRps")?
                                .ElementAnyNs("Numero").GetValue<string>() ?? string.Empty;
            GravarNFSeEmDisco(nfse.AsString(true), $"NFSe-{numeroNFSe}-{chaveNFSe}-.xml", dataNFSe);
            var nota = notas.FirstOrDefault(x => x.IdentificacaoRps.Numero == numeroRps);
            if (nota == null)
            {
                nota = notas.Load(nfse.ToString());
            }
            else
            {
                nota.IdentificacaoNFSe.Numero = numeroNFSe;
                nota.IdentificacaoNFSe.Chave = chaveNFSe;
                nota.IdentificacaoNFSe.DataEmissao = dataNFSe;
                nota.XmlOriginal = nfse.ToString();
            }
            retornoWebservice.Nota = nota;
            retornoWebservice.Sucesso = true;
        }

        private void MensagemErro(RetornoWebservice retornoWs, XContainer xmlRet, string xmlTag)
        {
            var mensagens = xmlRet?.ElementAnyNs(xmlTag);
            mensagens = mensagens?.ElementAnyNs("ListaMensagemRetorno");
            if (mensagens != null)
            {
                foreach (var mensagem in mensagens.ElementsAnyNs("MensagemRetorno"))
                {
                    var evento = new Evento
                    {
                        Codigo = mensagem?.ElementAnyNs("Codigo")?.GetValue<string>() ?? string.Empty,
                        Descricao = mensagem?.ElementAnyNs("Mensagem")?.GetValue<string>() ?? string.Empty,
                        Correcao = mensagem?.ElementAnyNs("Correcao")?.GetValue<string>() ?? string.Empty
                    };
                    retornoWs.Erros.Add(evento);
                }
            }
        }

        protected override void PrepararConsultarNFSe(RetornoConsultarNFSe retornoWebservice)
        {
            var message = new StringBuilder();
            message.Append("<ConsultarNfseFaixaEnvio xmlns=\"http://www.agili.com.br/nfse_v_1.00.xsd\">");
            message.Append($"<UnidadeGestora>{Municipio.Cnpj}</UnidadeGestora>");
            message.Append(MontarXmlIdentificacaoPrestador());
            message.Append($"<NumeroNfseInicial>{retornoWebservice.NumeroNFse}</NumeroNfseInicial>");
            message.Append($"<NumeroNfseFinal>{retornoWebservice.NumeroNFse}</NumeroNfseFinal>");
            message.Append("<Versao>1.00</Versao>");
            message.Append("</ConsultarNfseFaixaEnvio>");
            retornoWebservice.XmlEnvio = message.ToString();
        }

        protected override void AssinarConsultarNFSe(RetornoConsultarNFSe retornoWebservice)
        {
            return;
        }

        protected override void TratarRetornoConsultarNFSe(RetornoConsultarNFSe retornoWebservice, NotaServicoCollection notas)
        {
            // Analisa mensagem de retorno
            var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
            MensagemErro(retornoWebservice, xmlRet, "ConsultarNfseFaixaResposta");
            if (retornoWebservice.Erros.Any()) return;

            var retornoLote = xmlRet.ElementAnyNs("ConsultarNfseFaixaResposta");
            var listaNfse = retornoLote?.ElementAnyNs("ListaNfse");
            if (listaNfse == null)
            {
                retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "Lista de NFSe não encontrada! (ListaNfse)" });
                return;
            }
            var notasServico = new List<NotaServico>();
            foreach (var nfse in listaNfse.ElementsAnyNs("Nfse"))
            {
                var numeroNFSe = nfse.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty;
                var chaveNFSe = nfse.ElementAnyNs("CodigoAutenticidade")?.GetValue<string>() ?? string.Empty;
                var dataNFSe = nfse.ElementAnyNs("DataEmissao")?.GetValue<DateTime>() ?? DateTime.Now;
                var numeroRps = nfse.ElementAnyNs("DeclaracaoPrestacaoServico")?
                                    .ElementAnyNs("Rps")?
                                    .ElementAnyNs("IdentificacaoRps")?
                                    .ElementAnyNs("Numero").GetValue<string>() ?? string.Empty;
                GravarNFSeEmDisco(nfse.AsString(true), $"NFSe-{numeroNFSe}-{chaveNFSe}-.xml", dataNFSe);
                var nota = notas.FirstOrDefault(x => x.IdentificacaoRps.Numero == numeroRps);
                if (nota == null)
                {
                    nota = notas.Load(nfse.ToString());
                }
                else
                {
                    nota.IdentificacaoNFSe.Numero = numeroNFSe;
                    nota.IdentificacaoNFSe.Chave = chaveNFSe;
                    nota.IdentificacaoNFSe.DataEmissao = dataNFSe;
                    nota.XmlOriginal = nfse.ToString();
                }
                notasServico.Add(nota);
                notas.Add(nota);
            }
            retornoWebservice.Notas = notasServico.ToArray();
            retornoWebservice.Sucesso = true;
        }

        protected override void PrepararEnviarSincrono(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
        {
            // Fabio 09/09/2023 18:02 - O envio não será feito por Lote.
            if (notas.Count == 0)
            {
                retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "RPS não informado." });
                return;
            }
            var xmlRpsEnvio = new StringBuilder();
            xmlRpsEnvio.Append("<GerarNfseEnvio xmlns=\"http://www.agili.com.br/nfse_v_1.00.xsd\">");
            xmlRpsEnvio.Append($"<UnidadeGestora>{Municipio.Cnpj}</UnidadeGestora>");
            xmlRpsEnvio.Append("<DeclaracaoPrestacaoServico>");
            xmlRpsEnvio.Append(MontarXmlIdentificacaoPrestador());
            xmlRpsEnvio.Append(MontarXmlIdentificacaoRps(notas[0]));
            xmlRpsEnvio.Append(MontarXmlDadosTomador(notas[0]));
            xmlRpsEnvio.Append(MontarXmlDadosIntermediario(notas[0])); //Método não implementado
            xmlRpsEnvio.Append(MontarXmlRegimeEspecialTributacao(notas[0]));
            xmlRpsEnvio.Append(MontarXmlResponsavelRetencao(notas[0]));
            //xmlRpsEnvio.Append($"<CodigoAtividadeEconomica>{notas[0].Servico.ItemListaServico}</CodigoAtividadeEconomica>"); //Não é obrigatório
            xmlRpsEnvio.Append($"<ItemLei116AtividadeEconomica>{notas[0].Servico.ItemListaServico}</ItemLei116AtividadeEconomica>"); //Não é obrigatório
            xmlRpsEnvio.Append(MontarXmlExigibilidadeIssqn(notas[0]));
            xmlRpsEnvio.Append(MontarXmlMunicipioIncidencia(notas[0]));
            xmlRpsEnvio.Append(MontarXmlValoresRps(notas[0]));
            xmlRpsEnvio.Append(MontarXmlListaServico(notas[0]));
            xmlRpsEnvio.Append("<Versao>1.00</Versao>");
            xmlRpsEnvio.Append("</DeclaracaoPrestacaoServico>");
            xmlRpsEnvio.Append("</GerarNfseEnvio>");
            retornoWebservice.XmlEnvio = xmlRpsEnvio.ToString();
        }

        protected override void AssinarEnviarSincrono(RetornoEnviar retornoWebservice)
        {
            return;
        }

        protected override void TratarRetornoEnviarSincrono(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
        {
            var xmlRet = XDocument.Parse(retornoWebservice.XmlRetorno);
            MensagemErro(retornoWebservice, xmlRet, "GerarNfseResposta");
            if (retornoWebservice.Erros.Any()) return;

            var nfse = xmlRet.ElementAnyNs("Nfse");
            if (nfse == null)
            {
                retornoWebservice.Erros.Add(new Evento { Codigo = "0", Descricao = "NFSe não encontrada! (ListaNfse)" });
                return;
            }
            retornoWebservice.Sucesso = true; //retornou a nota
            retornoWebservice.Data = nfse.ElementAnyNs("DataEmissao")?.GetValue<DateTime>() ?? DateTime.MinValue;
            //retornoWebservice.Protocolo = xmlRet.Root?.ElementAnyNs("Protocolo")?.GetValue<string>() ?? string.Empty; //Não tem protocolo
            var numeroNFSe = nfse.ElementAnyNs("Numero")?.GetValue<string>() ?? string.Empty;
            var chaveNFSe = nfse.ElementAnyNs("CodigoAutenticidade")?.GetValue<string>() ?? string.Empty;
            var dataNFSe = nfse.ElementAnyNs("DataEmissao")?.GetValue<DateTime>() ?? DateTime.Now;
            var numeroRps = nfse.ElementAnyNs("DeclaracaoPrestacaoServico")?
                                .ElementAnyNs("Rps")?
                                .ElementAnyNs("IdentificacaoRps")?
                                .ElementAnyNs("Numero").GetValue<string>() ?? string.Empty;
            GravarNFSeEmDisco(nfse.AsString(true), $"NFSe-{numeroNFSe}-{chaveNFSe}-.xml", dataNFSe);
            var nota = notas.FirstOrDefault(x => x.IdentificacaoRps.Numero == numeroRps);


            if (nota == null)
            {
                nota = notas.Load(nfse.ToString());
            }
            else
            {
                nota.IdentificacaoNFSe.Numero = numeroNFSe;
                nota.IdentificacaoNFSe.Chave = chaveNFSe;
                nota.IdentificacaoNFSe.DataEmissao = dataNFSe;
                nota.XmlOriginal = nfse.ToString();
            }
        }

        public override NotaServico LoadXml(XDocument xmlNfse)
        {
            var ret = new NotaServico(Configuracoes)
            {
                XmlOriginal = xmlNfse.AsString()
            };
            LoadNFSe(ret, xmlNfse.Root);
            return ret;
        }


        #region not implemented
        public override string WriteXmlNFSe(NotaServico nota, bool identado = true, bool showDeclaration = true)
        {
            throw new NotImplementedException();
        }

        public override string WriteXmlRps(NotaServico nota, bool identado = true, bool showDeclaration = true)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarCancelarNFSe(RetornoCancelar retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarCancelarNFSeLote(RetornoCancelarNFSeLote retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarConsultarLoteRps(RetornoConsultarLoteRps retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarConsultarSequencialRps(RetornoConsultarSequencialRps retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarConsultarSituacao(RetornoConsultarSituacao retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarEnviar(RetornoEnviar retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void AssinarSubstituirNFSe(RetornoSubstituirNFSe retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararCancelarNFSe(RetornoCancelar retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararCancelarNFSeLote(RetornoCancelarNFSeLote retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararConsultarLoteRps(RetornoConsultarLoteRps retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararConsultarSequencialRps(RetornoConsultarSequencialRps retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararConsultarSituacao(RetornoConsultarSituacao retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararEnviar(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void PrepararSubstituirNFSe(RetornoSubstituirNFSe retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoCancelarNFSe(RetornoCancelar retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoCancelarNFSeLote(RetornoCancelarNFSeLote retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoConsultarLoteRps(RetornoConsultarLoteRps retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoConsultarSequencialRps(RetornoConsultarSequencialRps retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoConsultarSituacao(RetornoConsultarSituacao retornoWebservice)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoEnviar(RetornoEnviar retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }

        protected override void TratarRetornoSubstituirNFSe(RetornoSubstituirNFSe retornoWebservice, NotaServicoCollection notas)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
