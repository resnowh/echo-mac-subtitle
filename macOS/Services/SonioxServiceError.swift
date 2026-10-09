import Foundation

struct SonioxServiceError {
    let statusCode: Int?
    let errorType: String?
    let errorMessage: String
    let requestID: String?

    init?(response: [String: Any]) {
        guard response["error_message"] != nil
                || response["error_code"] != nil
                || response["error_type"] != nil else { return nil }
        statusCode = response["error_code"] as? Int
        errorType = Self.sanitize(response["error_type"] as? String ?? "")
        errorMessage = Self.sanitize(response["error_message"] as? String ?? "Soniox 返回了未说明的服务错误。")
        requestID = Self.sanitize(response["request_id"] as? String ?? "")
    }

    var userMessage: String {
        let guidance: String
        switch errorType ?? "" {
        case "unauthenticated":
            guidance = "Soniox API Key 无效或已过期，请在设置中更新。"
        case "organization_balance_exhausted", "organization_monthly_budget_exhausted", "project_monthly_budget_exhausted":
            guidance = "Soniox 账户余额或月度预算已用尽，请检查控制台中的余额和预算。"
        case "permission_denied":
            guidance = "此 Soniox API Key 没有实时语音识别权限，请在控制台检查 Key 权限。"
        case "temp_api_key_session_expired":
            guidance = "Soniox 临时 API Key 已达到会话时限，请更新 Key 后重新开始。"
        case "limit_exceeded", "concurrent_requests_limit_exceeded":
            guidance = "Soniox 使用或并发限额已达到，请等待限额恢复或结束其他会话。"
        case "request_timeout":
            guidance = "Soniox 请求超时，请检查网络后重试。"
        default:
            switch statusCode ?? 0 {
            case 401: guidance = "Soniox API Key 无效或已过期，请在设置中更新。"
            case 402: guidance = "Soniox 账户余额或月度预算已用尽，请检查控制台中的余额和预算。"
            case 403: guidance = "此 Soniox API Key 没有实时语音识别权限，请在控制台检查 Key 权限。"
            case 429: guidance = "Soniox 使用或并发限额已达到，请等待限额恢复或结束其他会话。"
            case 408, 500...599: guidance = "Soniox 服务暂时不可用，请检查网络后重试。"
            default: guidance = errorMessage
            }
        }

        var parts = [guidance]
        if !errorMessage.isEmpty && errorMessage != guidance {
            parts.append("服务说明：\(errorMessage)")
        }
        if let requestID, !requestID.isEmpty {
            parts.append("request_id: \(requestID)")
        }
        return parts.joined(separator: "\n")
    }

    private static func sanitize(_ value: String) -> String {
        let flattened = value.unicodeScalars.map { scalar -> String in
            if CharacterSet.newlines.contains(scalar) { return " " }
            if CharacterSet.controlCharacters.contains(scalar) { return "" }
            return String(scalar)
        }.joined()
        return flattened.split(whereSeparator: \.isWhitespace).joined(separator: " ")
    }
}
