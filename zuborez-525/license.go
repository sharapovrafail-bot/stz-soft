package main

// Лицензия убрана: программа бесплатная, без пробного периода и ключей.

type LicStatus struct {
	State    string `json:"state"`
	DaysLeft int    `json:"daysLeft"`
	ID       string `json:"id"`
	Msg      string `json:"msg"`
}

func LicCheck() LicStatus     { return LicStatus{State: "free"} }
func LicOK() bool             { return true }
func LicActivate(string) bool { return true }
