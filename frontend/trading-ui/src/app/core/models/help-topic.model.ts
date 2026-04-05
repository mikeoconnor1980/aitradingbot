export interface HelpTopic {
  id: string;
  title: string;
  icon: string;
  content: string;
}

export interface HelpChatMessage {
  role: "user" | "assistant";
  content: string;
  timestamp: Date;
}

export interface HelpChatRequest {
  question: string;
}

export interface HelpChatResponse {
  answer: string;
}
