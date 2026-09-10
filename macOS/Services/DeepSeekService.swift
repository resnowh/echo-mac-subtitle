import Foundation

struct DeepSeekService {
    let endpoint = URL(string: "https://api.deepseek.com/chat/completions")!

    func request(apiKey: String, prompt: String) -> URLRequest? {
        let body: [String: Any] = [
            "model": "deepseek-v4-flash",
            "messages": [
                ["role": "system", "content": "你是一个专业的会议和演讲总结助手。请用简体中文回答。"],
                ["role": "user", "content": prompt]
            ],
            "thinking": ["type": "disabled"],
            "stream": false,
            "max_tokens": 1200
        ]
        guard let bodyData = try? JSONSerialization.data(withJSONObject: body) else { return nil }
        var request = URLRequest(url: endpoint)
        request.httpMethod = "POST"
        request.timeoutInterval = 90
        request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.httpBody = bodyData
        return request
    }
}
