using System.Text.Json;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Infrastructure.Agents;

public class AgentService : IAgentService
{
    private const string WriterPrompt = """
        Jesteś pomocnikiem sklepu PetWorld. Pomagasz klientom wybrać produkty dla zwierząt domowych.

        Katalog produktów:
        - Royal Canin Adult Dog 15kg | Karma dla psów | 289 zł | Premium karma dla dorosłych psów średnich ras
        - Whiskas Adult Kurczak 7kg | Karma dla kotów | 129 zł | Sucha karma dla dorosłych kotów z kurczakiem
        - Tetra AquaSafe 500ml | Akwarystyka | 45 zł | Uzdatniacz wody do akwarium, neutralizuje chlor
        - Trixie Drapak XL 150cm | Akcesoria dla kotów | 399 zł | Wysoki drapak z platformami i domkiem
        - Kong Classic Large | Zabawki dla psów | 69 zł | Wytrzymała zabawka do napełniania smakołykami
        - Ferplast Klatka dla chomika | Gryzonie | 189 zł | Klatka 60x40cm z wyposażeniem
        - Flexi Smycz automatyczna 8m | Akcesoria dla psów | 119 zł | Smycz zwijana dla psów do 50kg
        - Brit Premium Kitten 8kg | Karma dla kotów | 159 zł | Karma dla kociąt do 12 miesiąca życia
        - JBL ProFlora CO2 Set | Akwarystyka | 549 zł | Kompletny zestaw CO2 dla roślin akwariowych
        - Vitapol Siano dla królików 1kg | Gryzonie | 25 zł | Naturalne siano łąkowe, podstawa diety

        Odpowiadaj po polsku. Gdy pytanie dotyczy zakupu lub doboru produktu, zarekomenduj konkretny produkt z katalogu wraz z ceną.
        """;

    private const string CriticPrompt = """
        Oceniasz odpowiedzi asystenta sklepu PetWorld.
        Zwróć WYŁĄCZNIE JSON bez żadnych dodatkowych znaków, w formacie:
        {"approved": true, "feedback": ""}

        Odrzuć odpowiedź (approved: false) gdy:
        - odpowiedź nie dotyczy zadanego pytania
        - pytanie dotyczy zakupu produktu, ale brak konkretnej rekomendacji z ceną
        - odpowiedź jest krótsza niż 2 zdania i nie zawiera wystarczających informacji
        """;

    private readonly AIAgent _writer;
    private readonly AIAgent _critic;

    public AgentService(string apiKey, string model)
    {
        var client = new OpenAIClient(apiKey);

        _writer = client
            .GetChatClient(model)
            .AsAIAgent(name: "Writer", instructions: WriterPrompt);

        _critic = client
            .GetChatClient(model)
            .AsAIAgent(name: "Critic", instructions: CriticPrompt);
    }

    public async Task<AgentResult> GenerateResponseAsync(string question, CancellationToken cancellationToken = default)
    {
        string answer = string.Empty;
        string feedback = string.Empty;

        for (int iteration = 1; iteration <= 3; iteration++)
        {
            var writerInput = iteration == 1
                ? question
                : $"Pytanie klienta: {question}\n\nFeedback od recenzenta: {feedback}\n\nPopraw swoją odpowiedź uwzględniając feedback.";

            var writerResponse = await _writer.RunAsync(writerInput, null, null, cancellationToken);
            answer = writerResponse.Text ?? string.Empty;

            var criticInput = $"Pytanie klienta: {question}\n\nOdpowiedź asystenta: {answer}";
            var criticResponse = await _critic.RunAsync(criticInput, null, null, cancellationToken);
            var criticText = criticResponse.Text ?? string.Empty;

            var criticResult = ParseCriticResponse(criticText);
            if (criticResult.Approved)
                return new AgentResult(answer, iteration);

            feedback = criticResult.Feedback;
        }

        return new AgentResult(answer, 3);
    }

    private static CriticResult ParseCriticResponse(string response)
    {
        try
        {
            var cleaned = response.Trim().Trim('`');
            if (cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[4..].Trim();
            return JsonSerializer.Deserialize<CriticResult>(cleaned)
                   ?? new CriticResult(true, string.Empty);
        }
        catch
        {
            return new CriticResult(true, string.Empty);
        }
    }
}
