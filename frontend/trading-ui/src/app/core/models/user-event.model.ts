import { FillEvent } from "./fill-event.model";
import { OrderUpdate } from "./order-update.model";

export type UserEventType = "Fill" | "OrderUpdate";

export interface UserEvent {
  type: UserEventType;
  timestamp: Date;
  data: FillEvent | OrderUpdate;
}
