import Foundation

enum SonioxErrorChecks {
    static func run() {
        let cases: [([String: Any], String, String)] = [
            (["error_code": 401, "error_type": "unauthenticated", "error_message": "Invalid API key", "request_id": "req-401"], "更新", "req-401"),
            (["error_code": 402, "error_type": "organization_balance_exhausted", "error_message": "Balance exhausted"], "余额", "Balance exhausted"),
            (["error_code": 403, "error_type": "permission_denied", "error_message": "Missing realtime permission"], "权限", "Missing realtime permission"),
            (["error_code": 429, "error_type": "limit_exceeded", "error_message": "Concurrent limit reached"], "限额", "Concurrent limit reached"),
            (["error_code": 408, "error_type": "request_timeout", "error_message": "Request timeout"], "超时", "Request timeout")
        ]

        for (payload, guidance, serviceMessage) in cases {
            guard let error = SonioxServiceError(response: payload) else {
                preconditionFailure("Soniox error response was not recognized")
            }
            precondition(error.userMessage.contains(guidance))
            precondition(error.userMessage.contains(serviceMessage))
        }

        let unsafe = SonioxServiceError(response: [
            "error_code": 401,
            "error_type": "unauthenticated",
            "error_message": "Invalid\r\nkey\u{0007}",
            "request_id": "req\r\nid"
        ])!
        precondition(!unsafe.errorMessage.contains("\r") && !unsafe.errorMessage.contains("\n"))
        precondition(unsafe.requestID == "req id")
        precondition(unsafe.userMessage.contains("Invalid key") && unsafe.userMessage.contains("req id"))
        guard case nil = SonioxServiceError(response: ["tokens": []]) else {
            preconditionFailure("A normal transcript response must not be classified as a service error")
        }
        print("PASS: Soniox auth, balance, permission, quota, timeout guidance and sanitized diagnostics")
    }
}
